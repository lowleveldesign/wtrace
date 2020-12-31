namespace LowLevelDesign.WTrace.Tracing

open System
open LowLevelDesign.WTrace

type EventFilter =
| ProcessId of string * int32
| ProcessName of string * string
| EventName of string * string
| Details of string * string
| Path of string * string

type EventFilterSettings = {
    Filters : array<EventFilter>
    DropFilteredEvents : bool
}

type SqlPrm = SqliteParameter

module EventFilter =

    let numericFilterOperators = [| "EqualTo"; "NotEqualTo"; "GreaterThanOrEqualTo"; "LessThanOrEqualTo" |]
    let stringFilterOperators = [| "EqualTo"; "NotEqualTo"; "StartsWith"; "EndsWith"; "Contains" |]

    [<AutoOpen>]
    module private H =
        let createCheck op =
            match op with
            | "EqualTo" -> ( = )
            | "NotEqualTo" -> ( <> )
            | "GreaterThanOrEqualTo" -> ( >= )
            | "LessThanOrEqualTo" -> ( <= )
            | _ -> invalidArg "filter" "exc_invalid_filter_value"

        let createCheckString op =
            match op with
            | "EqualTo" -> fun a b -> String.Compare(a, b, StringComparison.OrdinalIgnoreCase) = 0
            | "NotEqualTo" -> fun a b -> String.Compare(a, b, StringComparison.OrdinalIgnoreCase) <> 0
            | "StartsWith" -> fun a b -> a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
            | "EndsWith" -> fun a b -> a.EndsWith(b, StringComparison.OrdinalIgnoreCase)
            | "Contains" -> fun a b -> a.IndexOf(b, StringComparison.OrdinalIgnoreCase) <> -1
            | _ -> invalidArg "filter" "exc_invalid_filter_value"

        let buildFilterFunction filter (tracedata : ITraceData) =
            match filter with
            | ProcessId (op, n) ->
                let check = createCheck op
                ("process", fun ev -> check ev.ProcessId n)
            | ProcessName (op, s) ->
                let check = createCheckString op
                ("processname", fun ev -> check (tracedata.FindProcess struct (ev.ProcessId, ev.TimeStamp)).ProcessName s)
            | EventName (op , s) ->
                let check = createCheckString op
                ("eventname", fun ev -> check ev.EventName s)
            | Path (op, s) ->
                let check = createCheckString op
                ("processname", fun ev -> check ev.Path s)
            | Details (op, s) ->
                let check = createCheckString op
                ("processname", fun ev -> check ev.Details s)

    let buildFilterFunction tracedata filters =
        let filterGroups =
            filters
            |> Seq.map (fun f -> buildFilterFunction f tracedata)
            |> Seq.groupBy (fun (category, _) -> category)
            |> Seq.map (fun (_, s) -> s |> Seq.map (fun (c, f) -> f) |> Seq.toArray)
            |> Seq.toArray

        fun ev ->
            filterGroups
            |> Array.forall (fun filterGroup -> filterGroup |> Array.exists (fun f -> f ev))

