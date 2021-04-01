
module LowLevelDesign.WTrace.TraceControl

open System
open System.Collections.Generic
open System.Diagnostics
open System.Reactive.Linq
open System.Reactive.Subjects
open System.Threading
open FSharp.Control.Reactive
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Tracing

let mutable lostEventsCount = 0
let sessionWaitEvent = new ManualResetEvent(false)

[<AutoOpen>]
module private H =
    let rundownWaitEvent = new ManualResetEvent(false)

    let updateStatus s =
        match s with
        | SessionRunning -> rundownWaitEvent.Set() |> ignore
        | SessionError msg ->
            printfn "ERROR: Error when starting the trace session\n%s" msg
            rundownWaitEvent.Set() |> ignore
            sessionWaitEvent.Set() |> ignore
        | SessionStopped n ->
            lostEventsCount <- n
            rundownWaitEvent.Set() |> ignore
            sessionWaitEvent.Set() |> ignore

    let createEtwObservable settings =
        let sessionSubscribe (o : IObserver<TraceEventWithFields>) (ct : CancellationToken) =
            async {
                // the ct token when cancelled should stop the trace session gracefully
                EtwTraceSession.start settings updateStatus o.OnNext ct
                return RxDisposable.Empty
            } |> Async.StartAsTask

        Observable.Create(sessionSubscribe)
        |> Observable.publish

    let initiateEtwSession (etwObservable : IConnectableObservable<'a>) (ct : CancellationToken) =
        let etwsub = etwObservable.Connect()
        let reg = ct.Register(fun () -> etwsub.Dispose())

        printfn "Preparing the realtime trace session. Please wait..."
        rundownWaitEvent.WaitOne() |> ignore

        printfn ""
        printfn "Tracing session started. Press Ctrl + C to stop it."

        Disposable.compose etwsub reg

    let isRundown ev = ev.EventName = "Process/DCStart"

    let onEvent (TraceEventWithFields (ev, _)) =
        let getPath v = if v = "" then "" else sprintf " '%s'" v
        let getDesc v = if v = "" then "" else sprintf " %s" v
        let result = if ev.Result = WinApi.eventStatusUndefined then ""
                     else sprintf " -> %s" (WinApi.getNtStatusDesc ev.Result)
        printfn "%s %s (%d.%d) %s%s%s%s" (ev.TimeStamp.ToString("HH:mm:ss.ffff")) ev.ProcessName ev.ProcessId
            ev.ThreadId ev.EventName (getPath ev.Path) (getDesc ev.Details) result

    let onError (ex : Exception) =
        printfn "ERROR: an error occured while collecting the trace - %s" (ex.ToString())


let traceSystemOnly ct =
    let settings = {
        Handlers = [| ProcessThread.createEtwHandler(); IsrDpc.createEtwHandler() |]
        EnableStacks = false
    }
    let etwObservable = createEtwObservable settings

    etwObservable
    |> Observable.subscribe TraceStatistics.processEvent
    |> ignore

    use sub = initiateEtwSession etwObservable ct
    WaitHandle.WaitAny([| ct.WaitHandle; sessionWaitEvent |]) |> ignore

let traceEverything ct handlers filter showSummary =
    let settings = {
        Handlers = handlers
        EnableStacks = false
    }

    let currentProcessPid = Process.GetCurrentProcess().Id

    let etwObservable = createEtwObservable settings

    etwObservable
    |> Observable.filter (fun (TraceEventWithFields (ev, _)) ->
                              (not (isRundown ev)) && ev.ProcessId <> currentProcessPid && filter ev)
    |> Observable.subscribeWithCallbacks onEvent onError ignore
    |> ignore


    if showSummary then
        etwObservable
        |> Observable.filter (fun (TraceEventWithFields (ev, _)) -> ev.ProcessId <> currentProcessPid)
        |> Observable.subscribe TraceStatistics.processEvent
        |> ignore

    use sub = initiateEtwSession etwObservable ct
    WaitHandle.WaitAny([| ct.WaitHandle; sessionWaitEvent |]) |> ignore


module private ProcessApi =
    // returns true if the process stopped by itself, false if the ct got cancelled
    let rec waitForProcessExit (ct : CancellationToken) hProcess =
        match WinApi.waitForProcessExit hProcess 500 with
        | Error err -> Error err
        | Ok processFinished ->
            if processFinished then
                Ok true
            elif ct.IsCancellationRequested then
                Ok false
            elif sessionWaitEvent.WaitOne(0) then
                Ok false
            else
                waitForProcessExit ct hProcess

    let traceProcess ct handlers filter showSummary includeChildren (pid, hProcess, hThread : WinApi.SHandle) =
        result {
            let settings = {
                Handlers = handlers
                EnableStacks = false
            }

            let processIds = HashSet<int32>()
            processIds.Add(pid) |> ignore

            let handleProcessStart evf =
                let (TraceEventWithFields (ev, flds)) = evf
                if ev.EventName = "Process/Start" then
                    let parentPid = FieldValues.getI32FieldValue flds "ParentID"
                    if processIds.Contains(parentPid) then
                        processIds.Add(ev.ProcessId) |> ignore

            let handleProcessStop evf =
                let (TraceEventWithFields (ev, _)) = evf
                if ev.EventName = "Process/Stop" then
                    processIds.Remove(ev.ProcessId) |> ignore

            let etwObservable = createEtwObservable settings

            if includeChildren then
                etwObservable
                |> Observable.subscribe handleProcessStart
                |> ignore

            let filteredObservable =
                etwObservable
                |> Observable.filter (fun (TraceEventWithFields (ev, _)) -> processIds.Contains(ev.ProcessId))

            filteredObservable
            |> Observable.filter (fun (TraceEventWithFields (ev, _)) ->
                                      (not (isRundown ev)) && filter ev)
            |> Observable.subscribeWithCallbacks onEvent onError ignore
            |> ignore

            if showSummary then
                filteredObservable
                |> Observable.subscribe TraceStatistics.processEvent
                |> ignore

            if includeChildren then
                etwObservable
                |> Observable.subscribe handleProcessStop
                |> ignore

            use sub = initiateEtwSession etwObservable ct

            if not hThread.IsInvalid then
                do! WinApi.resumeThread hThread

            let! processFinished = waitForProcessExit ct hProcess
            if processFinished then
                printfn "Process (%d) exited." pid

            hThread.Close()
            hProcess.Close()
        }


let traceNewProcess ct handlers filter showSummary newConsole includeChildren (args : list<string>) =
    result {
        Debug.Assert(args.Length > 0, "[TraceControl] invalid number of arguments")
        let! (pid, hProcess, hThread) = WinApi.startProcessSuspended args newConsole

        do! ProcessApi.traceProcess ct handlers filter showSummary includeChildren (pid, hProcess, hThread) 
    }

let traceRunningProcess ct handlers filter showSummary includeChildren pid =
    result {
        let! hProcess = WinApi.openRunningProcess pid

        do! ProcessApi.traceProcess ct handlers filter showSummary includeChildren (pid, hProcess, WinApi.SHandle.Invalid)
    }
