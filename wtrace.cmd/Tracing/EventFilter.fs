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
}

module EventFilter =

    let numericFilterOperators = [| "="; "<>"; ">="; "<=" |]
    let stringFilterOperators = [| "="; "<>"; ">="; "<="; "~" |]

    [<AutoOpen>]
    module private H =
        let createCheck op =
            match op with
            | "=" -> ( = )
            | "<>" -> ( <> )
            | ">=" -> ( >= )
            | "<=" -> ( <= )
            | _ -> invalidArg "filter" "exc_invalid_filter_value"

        let createCheckString op =
            match op with
            | "=" -> fun a b -> String.Compare(a, b, StringComparison.OrdinalIgnoreCase) = 0
            | "<>" -> fun a b -> String.Compare(a, b, StringComparison.OrdinalIgnoreCase) <> 0
            | ">=" -> fun a b -> a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
            | "<=" -> fun a b -> a.EndsWith(b, StringComparison.OrdinalIgnoreCase)
            | "~" -> fun a b -> a.IndexOf(b, StringComparison.OrdinalIgnoreCase) <> -1
            | _ -> invalidArg "filter" "exc_invalid_filter_value"

        let buildFilterFunction filter =
            match filter with
            | ProcessId (op, n) ->
                let check = createCheck op
                ("process", fun ev -> check ev.ProcessId n)
            | ProcessName (op, s) ->
                let check = createCheckString op
                ("processname", fun ev -> check ev.ProcessName s)
            | EventName (op , s) ->
                let check = createCheckString op
                ("eventname", fun ev -> check ev.EventName s)
            | Path (op, s) ->
                let check = createCheckString op
                ("path", fun ev -> check ev.Path s)
            | Details (op, s) ->
                let check = createCheckString op
                ("details", fun ev -> check ev.Details s)

    let buildFilterFunction filters =
        let filterGroups =
            filters
            |> Seq.map (fun f -> buildFilterFunction f)
            |> Seq.groupBy (fun (category, _) -> category)
            |> Seq.map (fun (_, s) -> s |> Seq.map (fun (c, f) -> f) |> Seq.toArray)
            |> Seq.toArray

        fun ev ->
            filterGroups
            |> Array.forall (fun filterGroup -> filterGroup |> Array.exists (fun f -> f ev))

