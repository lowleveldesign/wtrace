namespace LowLevelDesign.WTrace

open System
open System.Threading
open FSharp.Control.Reactive
open System.Reactive.Linq
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Tracing

type RealtimeEventSource (processFilter, filterSettings) =

    let mutable lostEventsCount = 0

    let rundownWaitEvent = new ManualResetEvent(false)

    let filterEvent =
        let filterEvent = EventFilter.buildFilterFunction filterSettings.Filters
        fun (TraceEventWithFields (ev, _)) -> filterEvent ev

    let updateStatus s =
        let (SessionRunning s) = s
        Interlocked.Exchange(&lostEventsCount, s) |> ignore
        rundownWaitEvent.Set() |> ignore

    let realtimeObservable =

        let settings = {
            EnableStacks = false
        }

        let sessionSubscribe (o : IObserver<TraceEventWithFields>) (ct : CancellationToken) =
            async {
                // the ct token when cancelled should stop the trace session gracefully
                EtwTraceSession.start settings updateStatus o.OnNext ct
                return RxDisposable.Empty
            } |> Async.StartAsTask

        // we will keep last N events in the buffer
        Observable.Create(sessionSubscribe) |> Observable.publish

    let mutable subscription = RxDisposable.Empty

    member _.Start() =
        rundownWaitEvent.Reset() |> ignore

        subscription <- realtimeObservable.Connect()
        rundownWaitEvent.WaitOne() |> ignore

    member _.Stop () =
        subscription.Dispose()
        subscription <- RxDisposable.Empty

        // if the session was cancelled during rundown
        rundownWaitEvent.Set() |> ignore

    interface IObservable<TraceEventWithFields> with
        member _.Subscribe (o : IObserver<TraceEventWithFields>) =
            realtimeObservable
            |> Observable.filter filterEvent
            |> Observable.subscribeObserver o

    interface IDisposable with
        member _.Dispose () =
            subscription.Dispose()
            subscription <- RxDisposable.Empty

