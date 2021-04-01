namespace LowLevelDesign.WTrace.Tracing

open System
open LowLevelDesign.WTrace

type EventFilter =
| ProcessId of string * int32
| ProcessName of string * string
| EventName of string * string
| EventLevel of string * int32
| Details of string * string
| Path of string * string

type EventFilterSettings = {
    Filters : array<EventFilter>
}

module EventFilter =

    [<AutoOpen>]
    module private H =
        let createCheck op =
            match op with
            | "=" | "~" ->( = )
            | "<>" -> ( <> )
            | ">=" -> ( >= )
            | "<=" -> ( <= )
            | _ ->
                Debug.Assert(false, sprintf "Invalid filter operator '%s' for filter" op)
                ( = )

        let createCheckString op =
            match op with
            | "=" -> fun a b -> String.Compare(a, b, StringComparison.OrdinalIgnoreCase) = 0
            | "<>" -> fun a b -> String.Compare(a, b, StringComparison.OrdinalIgnoreCase) <> 0
            | ">=" -> fun a b -> a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
            | "<=" -> fun a b -> a.EndsWith(b, StringComparison.OrdinalIgnoreCase)
            | "~" -> fun a b -> a.IndexOf(b, StringComparison.OrdinalIgnoreCase) <> -1
            | _ -> 
                Debug.Assert(false, sprintf "Invalid filter operator '%s' for filter" op)
                ( = )

        let buildFilterFunction filter =
            match filter with
            | ProcessId (op, n) ->
                let check = createCheck op
                ("pid", fun ev -> check ev.ProcessId n)
            | ProcessName (op, s) ->
                let check = createCheckString op
                ("pname", fun ev -> check ev.ProcessName s)
            | EventName (op , s) ->
                let check = createCheckString op
                ("name", fun ev -> check ev.EventName s)
            | EventLevel (op , n) ->
                let check = createCheck op
                ("level", fun ev -> check ev.EventLevel n)
            | Path (op, s) ->
                let check = createCheckString op
                ("path", fun ev -> check ev.Path s)
            | Details (op, s) ->
                let check = createCheckString op
                ("details", fun ev -> check ev.Details s)

        let tryParseLevel v (n : outref<int32>) =
            if Int32.TryParse(v, &n) then true
            elif v === "debug" || v === "verbose" then n <- 5; true
            elif v === "info" then n <- 4; true
            elif v === "warning" then n <- 3; true
            elif v === "error" then n <- 2; true
            elif v === "critical" then n <- 1; true
            else n <- 0; false


    let buildFilterFunction filters =
        let filterGroups =
            filters
            |> Seq.map buildFilterFunction
            |> Seq.groupBy (fun (category, _) -> category)
            |> Seq.map (fun (_, s) -> s |> Seq.map (fun (c, f) -> f) |> Seq.toArray)
            |> Seq.toArray

        fun ev ->
            filterGroups
            |> Array.forall (fun filterGroup -> filterGroup |> Array.exists (fun f -> f ev))


    exception ParseError of string

    let parseFilter (filterStr : string) =
        let operators = [| "<>"; ">="; "<="; "~"; "=" |]

        match filterStr.Split(operators, 2, StringSplitOptions.None) with
        | [| filterName; filterValue |] ->
            let operator =
                if filterStr.[filterName.Length] = '=' then "="
                elif filterStr.[filterName.Length] = '~' then "~"
                else filterStr.Substring(filterName.Length, 2)
            let mutable n = 0
            let filterName = filterName.Trim()
            let filterValue = filterValue.Trim()
            if filterName === "pid" && Int32.TryParse(filterValue, &n) then
                ProcessId (operator, n)
            elif filterName === "level" && tryParseLevel filterValue &n then
                EventLevel (operator, n)
            elif filterName === "pname" then
                ProcessName (operator, filterValue)
            elif filterName === "name" then
                EventName (operator, filterValue)
            elif filterName === "path" then
                Path (operator, filterValue)
            elif filterName === "details" then
                Details (operator, filterValue)
            else raise (ParseError (sprintf "Invalid filter: '%s'" filterName))

        | [| eventName |] -> EventName ("~", eventName.Trim())
        | _ -> raise (ParseError (sprintf "Invalid filter definition: '%s'" filterStr))

    let printFilters filters =
        let buildFilterDescription filter =
            match filter with
            | ProcessId (op, n) -> ("Process ID", sprintf "%s '%d'" op n)
            | ProcessName (op, s) -> ("Process name", sprintf "%s '%s'" op s)
            | EventName (op , s) -> ("Event name", sprintf "%s '%s'" op s)
            | EventLevel (op , n) -> ("Event level", sprintf "%s '%d'" op n)
            | Path (op, s) -> ("Path", sprintf "%s '%s'" op s)
            | Details (op, s) -> ("Details", sprintf "%s '%s'" op s)

        let printFiltersGroup name defs =
            printfn "  %s" name
            printfn "    %s" (defs |> String.concat " OR ")

        filters
        |> Seq.map buildFilterDescription
        |> Seq.groupBy (fun (name, _) -> name)
        |> Seq.iter (fun (name, s) -> s |> Seq.map (fun (_, f) -> f)
                                        |> printFiltersGroup name)

