
module LowLevelDesign.WTrace.Program

open System
open System.Diagnostics
open System.Reflection
open System.Threading
open LowLevelDesign.WTrace.Tracing
open LowLevelDesign.WTrace

let className = "[main]"

let flags = [| "s"; "system"; "c"; "children"; "newconsole"; "nosummary"; "v"; "verbose"; "h"; "?"; "help" |]

let showHelp () =
    let appAssembly = Assembly.GetEntryAssembly();
    let appName = appAssembly.GetName();

    printfn "%s v%s - collects process or system traces" appName.Name (appName.Version.ToString())
    let customAttrs = appAssembly.GetCustomAttributes(typeof<AssemblyCompanyAttribute>, true);
    assert (customAttrs.Length > 0)
    printfn "Copyright (C) %d %s" DateTime.Today.Year (customAttrs.[0] :?> AssemblyCompanyAttribute).Company
    printfn ""
    printfn "Usage: %s [OPTIONS] pid|imagename args" appName.Name
    printfn @"
Options:
  -f, --filter=FILTER   Displays only events which satisfy a given FILTER.
                        (Does not impact the summary)
  -c, --children        Collects traces from the selected process and all its
                        children.
  --newconsole          Starts the process in a new console window.
  -s, --system          Collect only system statistics (Processes and DPC/ISR)
                        - shown in the summary.
  --nosummary           Prints only ETW events - no summary at the end.
  -v, --verbose         Shows wtrace diagnostics logs.
  -h, --help            Shows this message and exits.


  Each FILTER is built from a keyword, an operator and a value. You may
  define multiple events (filters with the same keywords are OR-ed).

  Keywords include: 
  - pid     - filtering on the proces ID
  - pname   - filtering on on the process name
  - name    - filtering on the event name
  - level   - filtering on the event level (0 [critical] - 5 [debug])
  - path    - filtering on the event path
  - details - filtering on the event details

  Operators include: =, <=, >=, ~ (contains), <> (does not equal)

  Example filters: -f 'pid = 1234', -f 'name ~ FileIO', -f 'level <= 4'
"

let isFlagEnabled args flags = flags |> Seq.exists (fun f -> args |> Map.containsKey f)

let parseFilters args =
    match args |> Map.tryFind "f" with
    | None -> Ok [| |]
    | Some filters ->
        try
            let parsedFilters = 
                filters
                |> List.map EventFilter.parseFilter
                |> List.toArray
            Ok parsedFilters
        with
        | :? ArgumentException as ex -> Error (ex.Message)

let checkElevated () = 
    if EtwTraceSession.isElevated () then Ok ()
    else Error "Must be elevated (Admin) to run this program."

let start (args : Map<string, list<string>>) = result {
    let isFlagEnabled = isFlagEnabled args
    let isInteger (v : string) = 
        let r, _ = Int32.TryParse(v)
        r

    do! checkElevated ()

    if [| "v"; "verbose" |] |> isFlagEnabled then
        Trace.AutoFlush <- true
        Logger.initialize(SourceLevels.Verbose, [ new TextWriterTraceListener(Console.Out) ])

    let! filters = parseFilters args
    let filterEvents = EventFilter.buildFilterFunction filters

    use cts = new CancellationTokenSource()

    Console.CancelKeyPress.Add(fun ev -> ev.Cancel <- true; cts.Cancel())

    match args |> Map.tryFind "" with 
    | None when [| "s"; "system" |] |> isFlagEnabled ->
        TraceControl.traceSystemOnly cts.Token

    | None ->
        TraceControl.traceEverything cts.Token filterEvents

    | Some args ->
        let newConsole = ([| "newconsole" |] |> isFlagEnabled)
        let includeChildren = [| "c"; "children" |] |> isFlagEnabled

        match args with
        | [ pid ] when isInteger pid -> do! TraceControl.traceRunningProcess cts.Token filterEvents includeChildren (Int32.Parse(pid))
        | args -> do! TraceControl.traceNewProcess cts.Token filterEvents newConsole includeChildren args

    printfn "Closing the trace session. Please wait..."
    if not (TraceControl.sessionWaitEvent.WaitOne(TimeSpan.FromSeconds(3.0))) then
        printfn "WARNING: the session did not finish in alotted time. Stop it manually: logman stop wtrace-rt -ets"

    if TraceControl.lostEventsCount > 0 then
        printfn "WARNING: %d events were lost in the session. Check wtrace help at https://wtrace.net to learn more." TraceControl.lostEventsCount

    if not ([| "nosummary" |] |> isFlagEnabled) then
        TraceStatistics.dumpStatistics ()
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

