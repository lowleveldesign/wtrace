namespace LowLevelDesign.WTrace.Tracing

open System
open FSharp.Control.Reactive
open System.Reflection
open System.Threading
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Session
open Microsoft.Diagnostics.Tracing.Parsers
open Microsoft.FSharp.Linq
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.ETW

module EtwTraceSession =

    [<AutoOpen>]
    module private H =

        [<Literal>]
        let className = "etw"

        let logger = Logger.EtwTracing

        let etwSessionName = "wtrace-rt"

        let providerFolder m (h, _) =
            let updateState m provider =
                let newp =
                    match m |> Map.tryFind provider.Id with
                    | None -> provider
                    | Some p -> { p with
                                    Level = if p.Level < provider.Level then p.Level else provider.Level
                                    Keywords = p.Keywords ||| provider.Keywords }
                m |> Map.add newp.Id newp
            h.Providers |> Array.fold updateState m

        let providerRundownFolder m (h, _) =
            let updateState m provider =
                let newp =
                    match m |> Map.tryFind provider.Id with
                    | None -> provider
                    | Some p -> { p with
                                    RundownLevel = if p.RundownLevel < provider.RundownLevel then p.RundownLevel else provider.RundownLevel
                                    RundownKeywords = p.RundownKeywords ||| provider.RundownKeywords }
                m |> Map.add newp.Id newp
            h.Providers |> Array.fold updateState m

        let prepareKernelParser (traceSessionSource : TraceEventSource) =
            let options = KernelTraceEventParser.ParserTrackingOptions.None
            let kernelParser = KernelTraceEventParser(traceSessionSource, options)

            // We want stateless session as ETW handlers have their own state
            let t = traceSessionSource.GetType()
            let kernelField = t.GetField("_Kernel", BindingFlags.Instance ||| BindingFlags.SetField ||| BindingFlags.NonPublic)
            kernelField.SetValue(traceSessionSource, kernelParser)

            traceSessionSource

        let runRundownSession handlersWithStates ct =

            let sessionName = sprintf "%s_rundown" etwSessionName
            logger.TraceInformation(sprintf "Starting rundown session %s" sessionName)

            use session = new TraceEventSession(sessionName)

            let kernelRundownFlags = handlersWithStates |> Array.fold (fun f (h, _) -> f ||| h.KernelRundownFlags) NtKeywords.None
            session.EnableKernelProvider(kernelRundownFlags, NtKeywords.None) |> ignore

            // Accessing the source enables kernel provider so must be run after the EnableKernelProvider call
            let eventSource = prepareKernelParser session.Source

            // Enable custom providers
            let providerOptions = TraceEventProviderOptions(StacksEnabled = false)
            handlersWithStates
            |> Array.fold providerRundownFolder Map.empty<Guid, EtwEventProvider>
            |> Map.iter (fun _ p -> session.EnableProvider(p.Id, p.RundownLevel, p.RundownKeywords, providerOptions) |> ignore)

            // For the rundown session, all the event Ids will be 0. We do not save them
            // anywhere (at least we should not)
            handlersWithStates
            |> Array.iter (fun (h, s) -> h.Subscribe (eventSource, true, (fun _ -> 0), id, s))

            // Rundown session lasts few secs - make it longer, if required
            use timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3.0))
            use cts = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct)
            use _r = cts.Token.Register(fun _ -> session.Stop() |> ignore)

            session.Source.Process() |> ignore
            logger.TraceInformation($"[{className}] Rundown session finished")

        let publishSessionConfigEvent publishMetaEvent (traceSource : TraceEventSource) =
            let t = traceSource.GetType()
            let fld = t.GetProperty("QPCFreq", BindingFlags.Instance ||| BindingFlags.GetProperty ||| BindingFlags.NonPublic)
            let qpcFreq = fld.GetValue(traceSource) :?> int64
            
            let fld = t.GetField("sessionStartTimeQPC", BindingFlags.Instance ||| BindingFlags.GetField ||| BindingFlags.NonPublic)
            let sessionStartQpc = fld.GetValue(traceSource) :?> int64

            publishMetaEvent (SessionConfig (traceSource.SessionStartTime, sessionStartQpc, qpcFreq))

    // This function starts the ETW session and initiates broadcasting trace events
    let start settings publishStatus publishMetaEvent publishTraceEvent (ct : CancellationToken) =

        let handlersWithStates =
            let eventBroadcast = {
                publishMetaEvent = publishMetaEvent
                publishTraceEvent = fun evf -> if settings.TraceFilter evf then publishTraceEvent evf
            }
            [| FileIO.createEtwHandler(); (* Registry.createEtwHandler() ; *) Rpc.createEtwHandler();
               ProcessThread.createEtwHandler(); TcpIp.createEtwHandler() |]
            |> Array.mapi (fun i h -> (h, h.Initialize (i, eventBroadcast)))
        let requiredKernelFlags = NtKeywords.Process ||| NtKeywords.Thread ||| NtKeywords.ImageLoad
        let kernelFlags = handlersWithStates |> Array.fold (fun f (h, _) -> f ||| h.KernelFlags) requiredKernelFlags
        let kernelStackFlags = if settings.EnableStacks then
                                   handlersWithStates |> Array.fold (fun f (h, _) -> f ||| h.KernelStackFlags) NtKeywords.None
                               else NtKeywords.None

        let providersMap =
            handlersWithStates |> Array.fold providerFolder Map.empty<Guid, EtwEventProvider>

        try
            logger.TraceInformation($"[{className}] Starting main ETW session")
            use session = new TraceEventSession("wtrace-rt")

            use _ctr = ct.Register(fun () -> session.Stop() |> ignore)

            session.EnableKernelProvider(kernelFlags, kernelStackFlags) |> ignore
            
            // session started so we can retrieve the necessary information
            publishSessionConfigEvent publishMetaEvent session.Source

            // Accessing the source enables kernel provider so must be run after the EnableKernelProvider call
            let eventSource = prepareKernelParser session.Source

            // Enable custom providers
            let providerOptions = TraceEventProviderOptions(StacksEnabled = settings.EnableStacks)
            providersMap |> Map.iter (fun _ p -> session.EnableProvider(p.Id, p.Level, p.Keywords, providerOptions) |> ignore)

            // Very simple Id generator for the session. It is never accessed asynchronously so there is no
            // risk if we simply increment it
            let mutable eventId = 0
            let idgen () = eventId <- eventId + 1; eventId

            // Subscribe handlers to the trace session
            handlersWithStates |> Array.iter (fun (h, s) -> h.Subscribe (eventSource, false, idgen, id, s))

            runRundownSession handlersWithStates ct

            do
                // send status message every second
                use _status =
                    Observable.interval (TimeSpan.FromSeconds(1.0))
                    |> Observable.subscribe (
                        fun _ ->
                            if session.IsActive then publishStatus (SessionRunning session.EventsLost))

                if (session.IsActive) then
                    session.Source.Process() |> ignore

            logger.TraceInformation($"[{className}] Main ETW session completed")
        with
        | ex ->
            logger.TraceError(ex)

    let isElevated () = TraceEventSession.IsElevated() ?= true
