[<AutoOpen>]
module LowLevelDesign.WTrace.Globals

open System
open System.Diagnostics
open System.Reactive.Linq
open System.Runtime.CompilerServices

[<assembly: InternalsVisibleTo("wtrace.tests")>] do ()

(* Constants *)

// The timeout for buffered observable - maximum amount of time that
// the buffered observable will wait for events
let eventBufferedObservableTimeout = TimeSpan.FromSeconds(0.5)

[<Literal>]
let invalidEventId = Int32.MinValue

(* Type aliases *)

type Debug = System.Diagnostics.Debug
type RxDisposable = System.Reactive.Disposables.Disposable

(* Class extensions *)

type TraceSource with
    member this.TraceError (ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, ex.ToString())
    
    member this.TraceErrorMessage (msg) =
        this.TraceEvent(TraceEventType.Error, 0, msg)

    member this.TraceErrorWithMessage (msg, ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, sprintf "%s\nDETAILS: %s" msg (ex.ToString()))

    member this.TraceWarning msg =
        this.TraceEvent(TraceEventType.Warning, 0, msg)

    member this.TraceWarningWithMessage (msg, ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, sprintf "%s\nDETAILS: %s" msg (ex.ToString()))

    member this.TraceVerbose msg =
        this.TraceEvent(TraceEventType.Verbose, 0, msg)


type Observable with
    /// Creates an observable sequence from the specified Subscribe method implementation.
    static member CreateEx (subscribe: IObserver<'T> -> unit -> unit) =
        let subscribe o = 
            let m = subscribe o
            Action(m)
        Observable.Create(subscribe)

module ObservableEx =

    let choose f source =
        Observable.Create (fun (o : IObserver<_>) ->
            FSharp.Control.Reactive.Observable.subscribeSafeWithCallbacks 
                (fun x -> ValueOption.iter o.OnNext (try f x with ex -> o.OnError ex; ValueNone))
                o.OnError
                o.OnCompleted
                source)

(* Operators and comp expressions *)

let result = ResultBuilder()

let (|?) lhs rhs = (if lhs = null then rhs else lhs)

let (===) a b = String.Equals(a, b, StringComparison.Ordinal)

(* Global loggers *)

module Logger =

    let Main = TraceSource("WTrace")
    let Tracing = TraceSource("WTrace.Tracing")
    let EtwTracing = TraceSource("WTrace.ETW.Tracing")
    let EtwEvents = TraceSource("WTrace.ETW.Events")


    module private H =

        let all = [| Main; Tracing; EtwTracing; EtwEvents |]

    do
        // remove the default logger - it's heavy
        H.all |> Seq.iter (fun s -> s.Listeners.Remove("Default"))

    let initialize (level : SourceLevels, listeners : list<TraceListener>) =
        H.all |> Seq.iter (fun s ->
                               s.Switch.Level <- level
                               listeners |> Seq.iter (s.Listeners.Add >> ignore))

