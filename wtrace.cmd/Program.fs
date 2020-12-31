
module LowLevelDesign.WTrace.Program

open System
open System.Diagnostics
open System.Reflection
open System.Reactive.Concurrency
open System.Threading
open FSharp.Control.Reactive
open LowLevelDesign.WTrace.Tracing
open LowLevelDesign.WTrace

let className = "[main]"

let flags = [| "s"; "system"; "c"; "children"; "newconsole"; "nosummary"; "withstacks"; "v"; "verbose"; "h"; "?"; "help" |]

let showHelp () =
    let appAssembly = Assembly.GetEntryAssembly();
    let appName = appAssembly.GetName();

    printfn "%s v%s - collects process traces" appName.Name (appName.Version.ToString())
    let customAttrs = appAssembly.GetCustomAttributes(typeof<AssemblyCompanyAttribute>, true);
    assert (customAttrs.Length > 0)
    printfn "Copyright (C) %d %s" DateTime.Today.Year (customAttrs.[0] :?> AssemblyCompanyAttribute).Company
    printfn ""
    printfn "Usage: %s [OPTIONS] tracefile|pid|imagename args" appName.Name
    printfn ""
    printfn "Options:"
    printfn "-f, --filter=FILTER   Displays only events which names contain the given keyword"
    printfn "                      (case insensitive). Does not impact the summary."
    // TODO: printfn "-s, --system          Collects only system statistics (DPC/ISR) - shown in the summary."
    printfn "-c, --children        Collects traces from the selected process and all its children."
    printfn "--newconsole          Starts the process in a new console window."
    printfn "--nosummary           Prints only ETW events - no summary at the end."
    // TODO: printfn "--withstacks          Collects data required to resolve stacks (memory consumption is much higher)."
    printfn "-v, --verbose         Shows wtrace diagnostics logs."
    printfn "-h, --help            Shows this message and exits."
    printfn ""

let isFlagEnabled args flags = flags |> Seq.exists (fun f -> args |> Map.containsKey f)

let checkElevated () = 
    if EtwTraceSession.isElevated () then Ok ()
    else Error "Must be elevated (Admin) to run this program."


let onEvent (tracedata : ITraceData) (TraceEventWithFields (ev, _)) =
    let getPath v = if v = "" then "" else sprintf " '%s'" v
    let getDesc v = if v = "" then "" else sprintf " %s" v
    let result = if ev.Result = WinApi.eventStatusUndefined then ""
                 else sprintf " -> %s" (WinApi.getNtStatusDesc ev.Result)
    printfn "%.4f (%d.%d) %s%s%s%s" ev.TimeStamp.TotalSeconds ev.ProcessId ev.ThreadId
        ev.EventName (getPath ev.Path) (getDesc ev.Details) result

let onError (ex : Exception) =
    printfn "ERROR: an error occured while collecting the trace - %s" (ex.ToString())

let startRealtime (source : RealtimeEventSource) stats (ct : CancellationToken) =
    let reg = ct.Register(fun () -> source.Stop()) :> IDisposable

    let onEvent = onEvent source.TraceData
    let eventSub = source
                   |> Observable.observeOn (new EventLoopScheduler())
                   |> Observable.subscribeWithCallbacks onEvent onError ignore

    let statSub = source
                  |> Observable.observeOn (new EventLoopScheduler())
                  |> Observable.subscribe (TraceStatistics.processEvent stats)

    printf "Preparing the realtime trace session. Please wait..."
    source.Start()
    printfn "done"

    [| reg; eventSub; statSub|] |> Disposables.compose

let rec waitForProcessExit (ct : CancellationToken) (proc : Diagnostics.Process) =
    if (not ct.IsCancellationRequested) && (not (proc.WaitForExit(500))) then
        waitForProcessExit ct proc
    else false

let start (args : Map<string, list<string>>) = result {
    let isFlagEnabled = isFlagEnabled args
    let isInteger (v : string) = 
        let r, _ = Int32.TryParse(v)
        r

    if [| "v"; "verbose" |] |> isFlagEnabled then
        Trace.AutoFlush <- true
        Logger.initialize(SourceLevels.Verbose, [ new TextWriterTraceListener(Console.Out) ])


    let filters =
        args |> Map.tryFind "f" |> Option.toList
        |> List.append (args |> Map.tryFind "filter" |> Option.toList)
        |> List.collect (id)
        |> List.map (fun s -> EventName ("Contains", s))
        |> List.toArray
    let filterSettings = {
        Filters = filters
        DropFilteredEvents = true
    }
    let withStacks = [| "withstacks" |] |> isFlagEnabled

    use cts = new CancellationTokenSource()
    let stats = TraceStatistics.create ()

    Console.CancelKeyPress.Add(fun ev -> ev.Cancel <- true; cts.Cancel())

    match args |> Map.tryFind "" with 
    | None ->
        use source = new RealtimeEventSource(NoFilter, filterSettings)
        use _sub = startRealtime source stats cts.Token
        cts.Token.WaitHandle.WaitOne() |> ignore

    | Some [ pid ] when isInteger pid ->
        let pid = Int32.Parse(pid)
        let processFilter = ProcessIdFilter (pid, [| "c"; "children" |] |> isFlagEnabled)
        do! checkElevated ()

        use proc = Process.GetProcessById(pid) // FIXME: this could throw exception ?

        use source = new RealtimeEventSource(processFilter, filterSettings)
        use _sub = startRealtime source stats cts.Token
        
        match (waitForProcessExit cts.Token proc) with
        | false -> ()
        | true -> source.Stop() // process stopped by itself - we need to close the session

    | Some args ->
        Debug.Assert(args.Length > 0, $"[%s{className}] invalid number of arguments")
        let fileName = args.[0]
        let options =
            ProcessStartInfo(fileName,
                             Arguments = (args |> Seq.skip 1 |> String.concat " "),
                             CreateNoWindow = not ([| "newconsole" |] |> isFlagEnabled))
        let proc = Process.Start(options)

        let processFilter = ProcessIdFilter (proc.Id, [| "c"; "children" |] |> isFlagEnabled)
        do! checkElevated ()

        use source = new RealtimeEventSource(processFilter, filterSettings)
        use _sub = startRealtime source stats cts.Token

        match (waitForProcessExit cts.Token proc) with
        | false -> ()
        | true -> source.Stop() // process stopped by itself - we need to close the session

    if not ([| "nosummary" |] |> isFlagEnabled) then
        TraceStatistics.dumpStatistics stats
}

let main (argv : array<string>) =
    let args = argv |> CommandLine.parseArgs flags

    if [| "h"; "help"; "?" |] |> isFlagEnabled args then
        showHelp ()
        0
    else
        match start args with
        | Ok _ -> 0
        | Error msg -> printfn "ERROR: %s" msg; 1

