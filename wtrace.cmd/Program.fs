﻿
module LowLevelDesign.WTrace.Program

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Threading
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Tracing
open LowLevelDesign.WTrace.Processing

let className = "[main]"

let appAssembly = Assembly.GetEntryAssembly()
let appName = appAssembly.GetName()

let showCopyright () =
    printfn ""
    printfn "%s v%s - collects process or system traces" appName.Name (appName.Version.ToString())
    let customAttrs = appAssembly.GetCustomAttributes(typeof<AssemblyCompanyAttribute>, true);  
    assert (customAttrs.Length > 0)
    printfn "Copyright (C) 2022 %s" (customAttrs.[0] :?> AssemblyCompanyAttribute).Company
    printfn "Visit https://wtrace.net to learn more"
    printfn ""

let showHelp () =
    printfn "Usage: %s [OPTIONS] [pid|imagename args]" appName.Name
    printfn @"
Options:
  -f, --filter=FILTER   Displays only events which satisfy a given FILTER.
                        (Does not impact the summary)
  --handlers=HANDLERS   Enable only specific event handlers
  -c, --children        Collects traces from the selected process and all its
                        children.
  --newconsole          Starts the process in a new console window.
  -s, --system          Collect only system statistics (Processes and DPC/ISR)
                        - shown in the summary.
  --symbols=SYMPATH     Resolve stacks and RPC method names using the provided symbols path.
  --nosummary           Prints only ETW events - no summary at the end.
  -v, --verbose         Shows wtrace diagnostics logs.
  -h, --help            Shows this message and exits.

  The HANDLERS parameter is a list of handler names, separated with a comma.

  Accepted handlers include:
    process   - only Process/Thread events (this handler is always enabled)
    file      - File I/O events
    registry  - Registry events (voluminous, disabled by default)
    rpc       - RPC events (enable image handler to allow RPC method name resolution)
    tcp       - TCP/IP events
    udp       - UDP events
    image     - image (module) events (load/unload)

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
        let resolveHandler (name : string) =
            if name === "process" then ProcessThread.createEtwHandler ()
            elif name === "file" then FileIO.createEtwHandler ()
            elif name === "registry" then Registry.createEtwHandler ()
            elif name === "rpc" then Rpc.createEtwHandler ()
            elif name === "tcp" then TcpIp.createEtwHandler ()
            elif name === "udp" then UdpIp.createEtwHandler ()
            elif name === "image" then Image.createEtwHandler ()
            else failwith (sprintf "Invalid handler name: '%s'" name)

        try
            let handlerNames = 
                handler.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun name -> name.Trim().ToLower())
                |> Set.ofArray
                |> Set.add "process" // process handler is always on
            let handlers = handlerNames |> Set.toArray |> Array.map resolveHandler

            printfn "HANDLERS"
            printfn "  %s" (handlerNames |> String.concat ", ")
            printfn ""

            Ok handlers
        with
        | Failure msg -> Error msg

    match args |> Map.tryFind "handlers" with
    | None -> createHandlers "process,image,file,rpc,tcp,udp"
    | Some [ handler ] ->
        if isSystemTrace args then
            Error ("Handlers are not allowed in the system trace.")
        else createHandlers handler
    | _ -> Error ("Handlers can be specified only once.")

let parseFilters args =
    let p filters =
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

    match args |> Map.tryFind "f" with
    | None ->
        match args |> Map.tryFind "filters" with
        | None -> Ok (fun _ -> true)
        | Some filters -> p filters
    | Some filters -> p filters

let checkElevated () = 
    if EtwTraceSession.isElevated () then Ok ()
    else Error "Must be elevated (Admin) to run this program."

let finishProcessingAndShowSummary tstate counters (ct : CancellationToken) =

    if RpcResolver.isRunning () then
        printf "\rResolving RPC endpoints (press Ctrl + C to stop) "
        while not ct.IsCancellationRequested && RpcResolver.isRunning () do
            printf "."
            Async.Sleep(500) |> Async.RunSynchronously 
        printfn ""

    TraceSummary.dump tstate counters

let start (supportFilesDirectory : string) (args : Map<string, list<string>>) = result {
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


    let dbgHelpPath =
        if IntPtr.Size = 4 then Path.Combine(supportFilesDirectory, "x86", "dbghelp.dll")
        else Path.Combine(supportFilesDirectory, "amd64", "dbghelp.dll")
    let debugSymbols =
        match args |> Map.tryFind "symbols" with
        | None ->
            match Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH") with
            | v when v <> null -> 
                printfn "Debug symbols path: %s" v
                printfn ""

                UseDbgHelp(dbgHelpPath, v)
            | _ -> DebugSymbolSettings.Ignore
        | Some paths ->
            Debug.Assert(paths.Length > 0)
            let symbolsPath = List.last paths

            printfn "Debug symbols path: %s" symbolsPath
            printfn ""

            UseDbgHelp(dbgHelpPath, symbolsPath)

    use tracingCts = new CancellationTokenSource()
    use processingCts = new CancellationTokenSource()

    let cancellationTokens : TraceControl.WorkCancellation = {
        TracingCancellationToken = tracingCts.Token
        ProcessingCancellationToken = processingCts.Token
    }

    Console.CancelKeyPress.Add(
        fun ev ->
            if not tracingCts.IsCancellationRequested then
                printfn "Closing the trace session. Please wait..."
                ev.Cancel <- true
                tracingCts.Cancel()
            elif not processingCts.IsCancellationRequested then
                ev.Cancel <- true
                processingCts.Cancel()
    )

    let showSummary = not ([| "nosummary" |] |> isFlagEnabled)

    let! traceState, counters =
        match args |> Map.tryFind "" with 
        | None when isSystemTrace args ->
            if not showSummary then
                printfn "WARNING: --nosummary does not take any effect in the system-only trace."
            TraceControl.traceSystemOnly cancellationTokens

        | None ->
            TraceControl.traceEverything cancellationTokens handlers filterEvents showSummary debugSymbols

        | Some args ->
            let newConsole = ([| "newconsole" |] |> isFlagEnabled)
            let includeChildren = [| "c"; "children" |] |> isFlagEnabled

            match args with
            | [ pid ] when isInteger pid ->
                TraceControl.traceRunningProcess cancellationTokens handlers filterEvents showSummary debugSymbols includeChildren (Int32.Parse(pid))
            | args ->
                TraceControl.traceNewProcess cancellationTokens handlers filterEvents showSummary debugSymbols newConsole includeChildren args

    if not (TraceControl.sessionWaitEvent.WaitOne(TimeSpan.FromSeconds(3.0))) then
        printfn "WARNING: the session did not finish in the allotted time. Stop it manually: logman stop wtrace-rt -ets"

    if TraceControl.lostEventsCount > 0 then
        printfn "WARNING: %d events were lost in the session. Check wtrace help at https://wtrace.net to learn more." TraceControl.lostEventsCount

    finishProcessingAndShowSummary traceState counters cancellationTokens.ProcessingCancellationToken
}

let main (supportFilesDirectory : string) (argv : array<string>) =
    let flags = [| "s"; "system"; "c"; "children"; "newconsole"; "nosummary"; "v"; "verbose"; "h"; "?"; "help" |]
    let args = argv |> CommandLine.parseArgs flags

    showCopyright ()

    if [| "h"; "help"; "?" |] |> isFlagEnabled args then
        showHelp ()
        0
    else
        match start supportFilesDirectory args with
        | Ok _ -> 0
        | Error msg -> printfn "ERROR: %s" msg; 1

