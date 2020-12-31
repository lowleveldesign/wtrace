namespace LowLevelDesign.WTrace.Events

open System.Collections.Generic
open LowLevelDesign.WTrace
open System

type IdGenerator = unit -> int32

type EventBroadcast = {
    publishTraceEvent : TraceEventWithFields -> unit
}

type internal DataCache<'K, 'V when 'K : equality> (capacity : int32) =

    let buffer = Array.create<'K> capacity Unchecked.defaultof<'K>
    let mutable currentIndex = 0
    let cache = Dictionary<'K, 'V>(capacity)

    do
        Debug.Assert(capacity > 0 && (capacity &&& (capacity - 1)) = 0, "[Cache] capacity must be power of 2 and must be greater than 0")

    member _.Add(k, v) =
        currentIndex <- (currentIndex + 1) &&& (capacity - 1)
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

module FieldValues =

    let inline getFieldValue fieldName fields =
        (fields |> Array.find (fun fld -> ordeq fld.FieldName fieldName)).FieldValue

    let inline i32db (n : int32) = sprintf "%d" n

    let inline ui32db (n : uint32) = sprintf "%d" n

    let inline i64db (n : int64) = sprintf "%d" n

    let inline ui64db (n : uint64) = sprintf "%d" n

    let inline date2db (d : DateTime) = sprintf "%d" d.Ticks

    let inline s2db (s : string) = s

    let inline guid2db (g : Guid) = g.ToString()

    let inline db2guid (b : string) = Guid(b)

    let inline db2i32 b = Int32.Parse(b)
    
    let inline db2ui32 b = UInt32.Parse(b)

    let inline db2i64 b = Int64.Parse(b)

    let inline db2ui64 b = UInt64.Parse(b)

    let inline db2date b = DateTime(Int64.Parse(b))

    let inline db2s b = b

