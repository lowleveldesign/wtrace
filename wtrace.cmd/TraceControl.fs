
module LowLevelDesign.WTrace.TraceControl

open System
open System.Diagnostics
open System.Reactive.Linq
open System.Reactive.Subjects
open System.Threading
open System.Text.Json
open FSharp.Control.Reactive
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Tracing
open LowLevelDesign.WTrace.Processing
open Windows.Win32
open Windows.Win32.Foundation

let mutable lostEventsCount = 0
let mutable lastEventTime = DateTime.MinValue.Ticks
let sessionWaitEvent = new ManualResetEvent(false)

type WorkCancellation = {
    TracingCancellationToken : CancellationToken
    ProcessingCancellationToken : CancellationToken
}

type OutputFormat =
| FreeText = 0
| Json = 1

[<AutoOpen>]
module private H =
    let rundownWaitEvent = new ManualResetEvent(false)

    let updateStatus s =
        match s with
        | SessionRunning -> rundownWaitEvent.Set() |> ignore
        | SessionError msg ->
            eprintfn "ERROR: Error when starting the trace session\n%s" msg
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

        eprintfn "Starting the tracing session (might take a moment). Press Ctrl + C to exit."
        rundownWaitEvent.WaitOne() |> ignore

        Disposable.compose etwsub reg

    let onEventJson (TraceEventWithFields (ev, _)) =
        printfn "%s" (JsonSerializer.Serialize(ev))
        Interlocked.Exchange(&lastEventTime, DateTime.Now.Ticks) |> ignore

    let onEvent (TraceEventWithFields (ev, _)) =
        let getPath v = if v = "" then "" else sprintf " '%s'" v
        let getDesc v = if v = "" then "" else sprintf " %s" v
        let result = if ev.Result = WinApi.eventStatusUndefined then ""
                     else sprintf " -> %s" (WinApi.getNtStatusDesc ev.Result)
        printfn "%s %s (%d.%d) %s%s%s%s" (ev.TimeStamp.ToString("HH:mm:ss.ffff")) ev.ProcessName ev.ProcessId
            ev.ThreadId ev.EventName (getPath ev.Path) (getDesc ev.Details) result
        Interlocked.Exchange(&lastEventTime, DateTime.Now.Ticks) |> ignore

    let onError (ex : Exception) =
        eprintfn "ERROR: an error occured while collecting the trace - %s" (ex.ToString())

let traceSystemOnly ct = 
    result {
        let settings = {
            Handlers = [| ProcessThread.createEtwHandler(); IsrDpc.createEtwHandler() |]
            EnableStacks = false
        }
        let etwObservable = createEtwObservable settings

        let tstate = TraceEventProcessor.init ProcessFilter.Everything Ignore ct.ProcessingCancellationToken
        let counters = TraceCounters.init ()

        etwObservable
        |> Observable.subscribe (TraceCounters.update tstate counters)
        |> ignore

        use sub = initiateEtwSession etwObservable ct.TracingCancellationToken
        WaitHandle.WaitAny([| ct.TracingCancellationToken.WaitHandle; sessionWaitEvent |]) |> ignore

        return (tstate, counters)
    }

let traceEverything ct handlers filter showSummary debugSymbols outputFormat =
    result {
        let settings = {
            Handlers = handlers
            EnableStacks = false
        }

        let etwObservable = createEtwObservable settings

        let tstate = TraceEventProcessor.init ProcessFilter.Everything debugSymbols ct.ProcessingCancellationToken

        let eventObservable =
            etwObservable
            |> Observable.filter (TraceEventProcessor.processAndFilterEvent tstate)

        let onEvent = if outputFormat = OutputFormat.Json then onEventJson else onEvent

        eventObservable
        |> Observable.filter (fun (TraceEventWithFields (ev, _)) -> filter ev)
        |> Observable.subscribeWithCallbacks onEvent onError ignore
        |> ignore

        let counters = TraceCounters.init ()

        if showSummary then
            eventObservable
            |> Observable.subscribe (TraceCounters.update tstate counters)
            |> ignore

        use sub = initiateEtwSession etwObservable ct.TracingCancellationToken
        WaitHandle.WaitAny([| ct.TracingCancellationToken.WaitHandle; sessionWaitEvent |]) |> ignore
        
        return (tstate, counters)
    }

module private ProcessApi =
    // returns true if the process stopped by itself, false if the ct got cancelled
    let rec waitForProcessExit (ct : CancellationToken) hProcess =
        match WinApi.waitForProcessExit hProcess 500u with
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

    let traceProcess ct handlers filter showSummary debugSymbols outputFormat includeChildren (pid, hProcess, hThread) =
        result {
            let settings = {
                Handlers = handlers
                EnableStacks = false
            }

            let onEvent = if outputFormat = OutputFormat.Json then onEventJson else onEvent

            let etwObservable = createEtwObservable settings
   
            let processFilter = ProcessFilter.Process (pid, includeChildren)
            let tstate = TraceEventProcessor.init processFilter debugSymbols ct.ProcessingCancellationToken
            let counters = TraceCounters.init ()

            let eventObservable =
                etwObservable
                |> Observable.filter (TraceEventProcessor.processAndFilterEvent tstate)

            eventObservable
            |> Observable.filter (fun (TraceEventWithFields (ev, _)) -> filter ev)
            |> Observable.subscribeWithCallbacks onEvent onError ignore
            |> ignore

            if showSummary then
                eventObservable
                |> Observable.subscribe (TraceCounters.update tstate counters)
                |> ignore

            use sub = initiateEtwSession etwObservable ct.TracingCancellationToken

            if hThread <> HANDLE.INVALID_HANDLE_VALUE then
                do! WinApi.resumeProcess hThread

            let! processFinished = waitForProcessExit ct.TracingCancellationToken hProcess
            if processFinished then
                eprintfn "Process (%d) exited." pid

            let mutable savedLastEventTime = Interlocked.Read(&lastEventTime)
            let rec waitForMoreEvents () =
                let t = Interlocked.Read(&lastEventTime)
                if t <> savedLastEventTime && (not ct.TracingCancellationToken.IsCancellationRequested) then
                    savedLastEventTime <- t
                    Thread.Sleep(1000)
                    waitForMoreEvents ()

            // when process exists too fast, we might miss some events
            // so we wait for a few seconds to prevent that
            Thread.Sleep(3000)
            waitForMoreEvents ()

            PInvoke.CloseHandle(hThread) |> ignore
            PInvoke.CloseHandle(hProcess) |> ignore

            return (tstate, counters)
        }


let traceNewProcess ct handlers filter showSummary debugSymbols outputFormat newConsole includeChildren (args : list<string>) =
    result {
        Debug.Assert(args.Length > 0, "[TraceControl] invalid number of arguments")
        let! processIds = WinApi.startProcessSuspended args newConsole

        return! ProcessApi.traceProcess ct handlers filter showSummary debugSymbols outputFormat includeChildren processIds
    }

let traceRunningProcess ct handlers filter showSummary debugSymbols outputFormat includeChildren pid =
    result {
        let! hProcess = WinApi.openRunningProcess pid
        let processIds = (pid, hProcess, HANDLE.INVALID_HANDLE_VALUE)

        return! ProcessApi.traceProcess ct handlers filter showSummary debugSymbols outputFormat includeChildren processIds
    }

