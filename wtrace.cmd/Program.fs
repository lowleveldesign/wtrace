
module LowLevelDesign.WTrace.Program

open System
open System.Diagnostics
open System.Reflection
open System.Threading
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Tracing

let className = "[main]"

let appAssembly = Assembly.GetEntryAssembly()
let appName = appAssembly.GetName()

let showCopyright () =
    printfn ""
    printfn "%s v%s - collects process or system traces" appName.Name (appName.Version.ToString())
    let customAttrs = appAssembly.GetCustomAttributes(typeof<AssemblyCompanyAttribute>, true);  
    assert (customAttrs.Length > 0)
    printfn "Copyright (C) %d %s" 2021 (customAttrs.[0] :?> AssemblyCompanyAttribute).Company
    printfn "Visit https://wtrace.net to learn more"
    printfn ""

let showHelp () =
    printfn "Usage: %s [OPTIONS] [pid|imagename args]" appName.Name
    printfn @"
Options:
  -f, --filter=FILTER   Displays only events which satisfy a given FILTER.
                        (Does not impact the summary)
  --handlers=HANDLERS   Displays only events coming from the specified HANDLERS.
  -c, --children        Collects traces from the selected process and all its
                        children.
  --newconsole          Starts the process in a new console window.
  -s, --system          Collect only system statistics (Processes and DPC/ISR)
                        - shown in the summary.
  --nosummary           Prints only ETW events - no summary at the end.
  -v, --verbose         Shows wtrace diagnostics logs.
  -h, --help            Shows this message and exits.

  The HANDLERS parameter is a list of handler names, separated with a comma.

  Accepted handlers include:
    process   - to receive Process/Thread events
    file      - to receive File I/O events
    registry  - to receive Registry events (voluminous, disabled by default)
    rpc       - to receive RPC events
    tcp       - to receive TCP/IP events
    udp       - to receive UDP events

  Example: --handlers 'tcp,file,registry'

  Each FILTER is built from a keyword, an operator, and a value. You may
  define multiple events (filters with the same keywords are OR-ed).

  Keywords include: 
    pid     - filtering on the proces ID
    pname   - filtering on on the process name
    name    - filtering on the event name
    level   - filtering on the event level (1 [critical] - 5 [debug])
    path    - filtering on the event path
    details - filtering on the event details

  Operators include:
    =, <> (does not equal), <= (ends with), >= (starts with), ~ (contains)

  Example: -f 'pid = 1234', -f 'name ~ FileIO', -f 'level <= 4'
"

let isFlagEnabled args flags = flags |> Seq.exists (fun f -> args |> Map.containsKey f)

let isSystemTrace args = [| "s"; "system" |] |> isFlagEnabled args

let parseHandlers args =
    let createHandlers (handler : string) =
        let createHandler (name : string) =
            if name >=< "process" then ProcessThread.createEtwHandler ()
            elif name >=< "file" then FileIO.createEtwHandler ()
            elif name >=< "registry" then Registry.createEtwHandler ()
            elif name >=< "rpc" then Rpc.createEtwHandler ()
            elif name >=< "tcp" then TcpIp.createEtwHandler ()
            elif name >=< "udp" then UdpIp.createEtwHandler ()
            else failwith (sprintf "Invalid handler name: '%s'" name)

        try
            let handlerNames = 
                handler.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun name -> name.Trim().ToLower())
            let handlers = handlerNames |> Array.map createHandler

            printfn "HANDLERS"
            printfn "  %s" (handlerNames |> String.concat ", ")
            printfn ""

            Ok handlers
        with
        | Failure msg -> Error msg

    match args |> Map.tryFind "handlers" with
    | None -> createHandlers "process,file,rpc,tcp,udp"
    | Some [ handler ] ->
        if isSystemTrace args then
            Error ("Handlers are not allowed in the system trace.")
        else createHandlers handler
    | _ -> Error ("Handlers can be specified only once.")

let parseFilters args =
    match args |> Map.tryFind "f" with
    | None -> Ok (fun _ -> true)
    | Some filters ->
        if isSystemTrace args then
            Error ("Filters are not allowed in the system trace.")
        else
            try
                let filters =
                    filters |> List.map EventFilter.parseFilter
                printfn "FILTERS"
                if filters |> List.isEmpty then printfn "  [none]"
                else EventFilter.printFilters filters
                printfn ""
                Ok (EventFilter.buildFilterFunction filters)
            with
            | EventFilter.ParseError msg -> Error msg

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

    let! filterEvents = parseFilters args
    let! handlers = parseHandlers args

    use cts = new CancellationTokenSource()

    Console.CancelKeyPress.Add(fun ev -> ev.Cancel <- true; cts.Cancel())

    let showSummary = not ([| "nosummary" |] |> isFlagEnabled)

    match args |> Map.tryFind "" with 
    | None when isSystemTrace args ->
        if not showSummary then
            printfn "WARNING: --nosummary does not take any effect in the system-only trace."
        TraceControl.traceSystemOnly cts.Token

    | None ->
        TraceControl.traceEverything cts.Token handlers filterEvents showSummary

    | Some args ->
        let newConsole = ([| "newconsole" |] |> isFlagEnabled)
        let includeChildren = [| "c"; "children" |] |> isFlagEnabled

        match args with
        | [ pid ] when isInteger pid ->
            do! TraceControl.traceRunningProcess cts.Token handlers filterEvents showSummary includeChildren (Int32.Parse(pid))
        | args ->
            do! TraceControl.traceNewProcess cts.Token handlers filterEvents showSummary newConsole includeChildren args

    printfn "Closing the trace session. Please wait..."
    if not (TraceControl.sessionWaitEvent.WaitOne(TimeSpan.FromSeconds(3.0))) then
        printfn "WARNING: the session did not finish in the allotted time. Stop it manually: logman stop wtrace-rt -ets"

    if TraceControl.lostEventsCount > 0 then
        printfn "WARNING: %d events were lost in the session. Check wtrace help at https://wtrace.net to learn more." TraceControl.lostEventsCount

    TraceStatistics.dumpStatistics ()
}

let main (argv : array<string>) =
    let flags = [| "s"; "system"; "c"; "children"; "newconsole"; "nosummary"; "v"; "verbose"; "h"; "?"; "help" |]
    let args = argv |> CommandLine.parseArgs flags

    showCopyright ()

    if [| "h"; "help"; "?" |] |> isFlagEnabled args then
        showHelp ()
        0
    else
        match start args with
        | Ok _ -> 0
        | Error msg -> printfn "ERROR: %s" msg; 1

