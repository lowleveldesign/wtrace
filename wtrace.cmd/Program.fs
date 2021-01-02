
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

    printfn "%s v%s - collects process traces" appName.Name (appName.Version.ToString())
    let customAttrs = appAssembly.GetCustomAttributes(typeof<AssemblyCompanyAttribute>, true);
    assert (customAttrs.Length > 0)
    printfn "Copyright (C) %d %s" DateTime.Today.Year (customAttrs.[0] :?> AssemblyCompanyAttribute).Company
    printfn ""
    printfn "Usage: %s [OPTIONS] pid|imagename args" appName.Name
    printfn ""
    printfn "Options:"
    printfn "-f, --filter=FILTER   Displays only events which names contain the given keyword"
    printfn "                      (case insensitive). Does not impact the summary."
    printfn "-c, --children        Collects traces from the selected process and all its children."
    printfn "--newconsole          Starts the process in a new console window."
    printfn "-s, --system          Collect system statistics (DPC/ISR) - shown in the summary."
    printfn "--nosummary           Prints only ETW events - no summary at the end."
    printfn "-v, --verbose         Shows wtrace diagnostics logs."
    printfn "-h, --help            Shows this message and exits."
    printfn ""

let isFlagEnabled args flags = flags |> Seq.exists (fun f -> args |> Map.containsKey f)

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

    // FIXME: filters

    use cts = new CancellationTokenSource()

    Console.CancelKeyPress.Add(fun ev -> ev.Cancel <- true; cts.Cancel())

    match args |> Map.tryFind "" with 
    | None when [| "s"; "system" |] |> isFlagEnabled ->
        // FIXME trace system
        ()

    | None ->
        TraceControl.traceEverything cts.Token

    | Some args ->
        let newConsole = ([| "newconsole" |] |> isFlagEnabled)
        let includeChildren = [| "c"; "children" |] |> isFlagEnabled

        match args with
        | [ pid ] when isInteger pid -> do! TraceControl.traceRunningProcess cts.Token includeChildren (Int32.Parse(pid))
        | args -> do! TraceControl.traceNewProcess cts.Token newConsole includeChildren args

    printfn "Closing the session to complete. Please wait..."
    if not (TraceControl.sessionWaitEvent.WaitOne(TimeSpan.FromSeconds(2.0))) then
        printfn "WARNING: the session did not finish in alotted time. Stop it manually: logman stop wtrace-rt -ets"

    if TraceControl.lostEventsCount > 0 then
        printfn "WARNING: %d events were lost in the session. Check wtrace help at https://wtrace.net to learn more." TraceControl.lostEventsCount

    if not ([| "nosummary" |] |> isFlagEnabled) then
        TraceStatistics.dumpStatistics TraceControl.stats
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

