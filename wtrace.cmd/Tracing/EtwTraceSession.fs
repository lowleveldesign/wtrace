namespace LowLevelDesign.WTrace.Tracing

open System
open System.Reflection
open System.Threading
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Session
open Microsoft.Diagnostics.Tracing.Parsers
open Microsoft.FSharp.Linq
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events

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
            logger.TraceInformation(sprintf "[etw] Starting rundown session %s" sessionName)

            use session = new TraceEventSession(sessionName)

            // Rundown session lasts few secs - make it longer, if required
            use timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3.0))
            use cts = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct)
            use _r = cts.Token.Register(fun _ -> if (session.IsActive) then
                                                     session.Stop() |> ignore)

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
            |> Array.iter (fun (h, s) -> h.Subscribe (TraceEventSources(eventSource), true, (fun _ -> 0), s))

            if session.IsActive then
                session.Source.Process() |> ignore
            logger.TraceInformation(sprintf "[%s] Rundown session finished" className)

    // This function starts the ETW session and initiates broadcasting trace events
    let start settings publishStatus publishTraceEvent (ct : CancellationToken) =

        let handlersWithStates =
            let eventBroadcast = { publishTraceEvent = publishTraceEvent }
            settings.Handlers |> Array.map (fun h -> (h, h.Initialize eventBroadcast))
        let requiredKernelFlags = NtKeywords.Process ||| NtKeywords.Thread ||| NtKeywords.ImageLoad
        let kernelFlags = handlersWithStates |> Array.fold (fun f (h, _) -> f ||| h.KernelFlags) requiredKernelFlags
        let kernelStackFlags = if settings.EnableStacks then
                                   handlersWithStates |> Array.fold (fun f (h, _) -> f ||| h.KernelStackFlags) NtKeywords.None
                               else NtKeywords.None

        let providersMap =
            handlersWithStates |> Array.fold providerFolder Map.empty<Guid, EtwEventProvider>

        try
            logger.TraceInformation(sprintf "[%s] Starting main ETW session" className)
            use session = new TraceEventSession("wtrace-rt")

            let mutable eventsLost = 0
            use _ctr = ct.Register(fun () ->
                                        if session.IsActive then
                                            // save lost events for statistics
                                            eventsLost <- session.EventsLost
                                        session.Stop() |> ignore)

            session.EnableKernelProvider(kernelFlags, kernelStackFlags) |> ignore

            // Accessing the source enables kernel provider so must be run after the EnableKernelProvider call
            let eventSource = prepareKernelParser session.Source

            // Enable custom providers
            let providerOptions = TraceEventProviderOptions(StacksEnabled = settings.EnableStacks)
            providersMap |> Map.iter (fun _ p -> session.EnableProvider(p.Id, p.Level, p.Keywords, providerOptions) |> ignore)

            // Very simple Id generator for the session. It is never accessed asynchronously so there is no
            // risk if we simply increment it
            let mutable eventId = 0
            let idgen () = Interlocked.Increment(&eventId)

            // Subscribe handlers to the trace session
            handlersWithStates |> Array.iter (fun (h, s) -> h.Subscribe (TraceEventSources(eventSource), false, idgen, s))

            runRundownSession handlersWithStates ct

            if (session.IsActive) then
                publishStatus SessionRunning
                session.Source.Process() |> ignore

            publishStatus (SessionStopped eventsLost)
            logger.TraceInformation(sprintf "[%s] Main ETW session completed" className)
        with
        | ex ->
            publishStatus (SessionError (ex.ToString()))
            logger.TraceError(ex)

    let isElevated () = TraceEventSession.IsElevated() ?= true
