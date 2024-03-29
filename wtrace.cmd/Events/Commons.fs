﻿namespace LowLevelDesign.WTrace.Events

open System
open System.Collections.Generic
open System.Threading
open Microsoft.Diagnostics.Tracing

open LowLevelDesign.WTrace

type EtwEvent = Microsoft.Diagnostics.Tracing.TraceEvent

type NtKeywords = Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords

type IdGenerator = unit -> int32

type EventBroadcast = {
    publishTraceEvent : TraceEventWithFields -> unit
}

type EtwEventProvider = {
    Id : Guid
    Name : string
    Level : TraceEventLevel
    Keywords : uint64
    RundownLevel : TraceEventLevel
    RundownKeywords : uint64
}

type EtwEventHandler = {
    KernelRundownFlags : NtKeywords
    KernelFlags : NtKeywords
    KernelStackFlags : NtKeywords
    Providers : array<EtwEventProvider>

    Initialize : EventBroadcast -> obj (* handler state *)
    Subscribe : TraceEventSources *
                bool (* isRundown *) *
                IdGenerator (* generates unique ids for events *) *
                obj(* handler state *) -> unit
}

module FieldValues =

    let inline private getFieldValue fields fieldName =
        (fields |> Array.find (fun fld -> fld.FieldName = fieldName)).FieldValue

    let getI32FieldValue flds fname =
        match (getFieldValue flds fname) with
        | FI32 n -> n
        | _ -> invalidArg (nameof fname) ""

    let getI64FieldValue flds fname =
        match (getFieldValue flds fname) with
        | FI64 n -> n
        | _ -> invalidArg (nameof fname) ""

    let getUI64FieldValue flds fname =
        match (getFieldValue flds fname) with
        | FUI64 n -> n
        | _ -> invalidArg (nameof fname) ""

    let getTextFieldValue flds fname =
        match (getFieldValue flds fname) with
        | FText s -> s
        | _ -> invalidArg (nameof fname) ""

    let getF64FieldValue flds fname =
        match (getFieldValue flds fname) with
        | FF64 f -> f
        | _ -> invalidArg (nameof fname) ""

    let getGuidFieldValue flds fname =
        match (getFieldValue flds fname) with
        | FGuid f -> f
        | _ -> invalidArg (nameof fname) ""



module internal HandlerCommons =

    let noFields = Array.empty<TraceEventField>

    let handleEvent<'T, 'S when 'T :> EtwEvent> (idgen : IdGenerator) (state : 'S) handler (ev : 'T) : unit =
        handler (idgen()) state ev

    let handleEventNoId<'T, 'S when 'T :> EtwEvent> (state : 'S) handler (ev : 'T) : unit =
        handler state ev

    let toEventField eventId struct (fieldName, fieldValue) =
        {
            EventId = eventId
            FieldName =  fieldName
            FieldValue = fieldValue
        }

    let toEvent (ev : EtwEvent) eventId activityId path details result =
        {
            EventId = eventId
            TimeStamp = ev.TimeStamp
            ActivityId = activityId
            Duration = TimeSpan.Zero
            ProcessId = ev.ProcessID
            ProcessName = ev.ProcessName
            ThreadId = ev.ThreadID
            EventName = ev.EventName
            EventLevel = int32 ev.Level
            Path = path
            Details = details
            Result = result
        }

type internal DataCache<'K, 'V when 'K : equality> (capacity : int32) =

    let buffer = Array.create<'K> capacity Unchecked.defaultof<'K>
    let mutable currentIndex = 0
    let cache = Dictionary<'K, 'V>(capacity)

    member _.Add(k, v) =
        currentIndex <- (currentIndex + 1) % capacity
        let previousKey = buffer.[currentIndex]
        if previousKey <> Unchecked.defaultof<'K> then
            cache.Remove(previousKey) |> ignore
        cache.Add(k, v)
        buffer.[currentIndex] <- k
        Debug.Assert(cache.Count <= capacity, "[Cache] cache.Count < capacity")

    member _.Remove = cache.Remove

    member _.ContainsKey = cache.ContainsKey

    member _.TryGetValue = cache.TryGetValue

    member this.Item
        with get k =
            cache.[k]
        and set k v =
            if cache.ContainsKey(k) then
                cache.[k] <- v
            else
                this.Add(k, v)


type internal DataCacheWithCount<'K, 'V when 'K : equality> (capacity : int32) =

    let cache = DataCache<'K, 'V>(capacity)
    let counts = Dictionary<'K, int32>(capacity)

    member _.Add(k, v) =
        cache.Add(k, v)
        counts.Add(k, 1)

    member _.Remove k =
        match counts.TryGetValue(k) with
        | true, cnt when cnt = 1 ->
            cache.Remove(k) |> ignore
            counts.Remove(k) |> ignore
        | true, cnt ->
            counts.[k] <- cnt - 1
        | false, _ -> ()

    member _.ContainsKey = cache.ContainsKey

    member _.TryGetValue = cache.TryGetValue

    member this.Item
        with get k =
            cache.[k]
        and set k v =
            match counts.TryGetValue(k) with
            | true, cnt ->
                cache.[k] <- v
                counts.[k] <- cnt + 1
            | false, _ ->
                this.Add(k, v)

