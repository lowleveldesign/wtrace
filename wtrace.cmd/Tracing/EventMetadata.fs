namespace LowLevelDesign.WTrace.Tracing

open System
open System.Collections.Generic
open LowLevelDesign.WTrace
open System.Threading

type IEventMetadata =
    abstract GetFieldName : struct (int32 * int32) -> string // handlerId, fieldId
    abstract GetEventName : struct (Guid * int32 * int32) -> string // providerId, taskId, opcodeId
    abstract GetTasks : unit -> array<(Guid * string * int32 * string)>
    abstract GetOpcodes : unit -> array<(Guid * string * int32 * string * int32 * string)>
    abstract QpcToRelativeTimeInMs: Qpc -> float

type IMutableEventMetadata =
    inherit IEventMetadata

    abstract HandleMetadataEvent : MetadataEvent -> unit

module EventMetadata =

    [<AutoOpen>]
    module private H =
        type EventProviderMetadata = {
            Name : string
            Tasks : IDictionary<int32, string>
            Opcodes : IDictionary<struct (int32 * int32), struct (string * string)>
            EventNames : IDictionary<struct (int32 * int32), string>
        }
        type ProviderMap = Dictionary<Guid, EventProviderMetadata>
        type EventFieldNamesMap = Dictionary<struct (int32 * int32), string>

        let createProvider name =
            {
                Name = name
                Tasks = Dictionary<int32, string>()
                Opcodes = Dictionary<struct (int32 * int32), struct (string * string)>()
                EventNames = Dictionary<struct (int32 * int32), string>()
            }

        let qpcToRelativeTimeInMs sessionStartTimeQpc (qpcFreq: int64) (Qpc qpc) =
            // no need to lock here as we always create a new trace data when QPC is modified
            if (qpc < sessionStartTimeQpc) then
                0.0
            else
                float (qpc - sessionStartTimeQpc)
                * 1000.0
                / float qpcFreq

        let addOpcode (providers : IDictionary<Guid, EventProviderMetadata>) (providerId, taskId, opcodeId, opcodeName) =
            let provider = providers.[providerId]
            let taskName = provider.Tasks.[taskId]
            provider.Opcodes.[struct (taskId, opcodeId)] <- (taskName, opcodeName)
            provider.EventNames.[struct (taskId, opcodeId)] <- sprintf "%s/%s" taskName opcodeName

        let getFieldName (eventFieldNames : EventFieldNamesMap) k =
            match eventFieldNames.TryGetValue(k) with
            | (true, name) -> name
            | (false, _) -> invalidOp (sprintf "exc_field_id_missing: '%A'" k)

        let getEventName (providers : ProviderMap) struct (providerId, taskId, opcodeId) =
            match providers.TryGetValue(providerId) with
            | (false, _) -> sprintf "(%d)/(%d)" taskId opcodeId
            | (true, prov) ->
                match prov.EventNames.TryGetValue struct (taskId, opcodeId) with
                | (true, s) -> s
                | (false, _) -> sprintf "(%d)/(%d)" taskId opcodeId

        let getTasks (providers : ProviderMap) =
            providers
            |> Seq.map (fun pkv -> pkv.Value.Tasks |> Seq.map (fun tkv -> (pkv.Key, pkv.Value.Name, tkv.Key, tkv.Value)))
            |> Seq.concat
            |> Seq.toArray

        let getOpcodes (providers : ProviderMap) =
            providers
            |> Seq.map (fun pkv ->
                            let tasks = pkv.Value.Tasks
                            pkv.Value.Opcodes
                            |> Seq.map (fun opkv ->
                                            let struct (taskId, opcodeId) = opkv.Key
                                            let struct (taskName, opcodeName) = opkv.Value
                                            (pkv.Key, pkv.Value.Name, taskId, taskName, opcodeId, opcodeName)))
            |> Seq.concat
            |> Seq.toArray

    let createMutable () =
        let lck = obj()
        let eventFieldNames = EventFieldNamesMap()
        let providers = ProviderMap()
        let minQpc = qpcToInt64 QpcMin
        let mutable sessionStartTimeQpc = minQpc
        let mutable sessionStartTimeUtc = DateTime.MinValue.Ticks
        let mutable qpcFreq = 1L

        {
            new IMutableEventMetadata with
                member _.GetFieldName struct (taskId, opcodeId) =
                    lock lck (fun () -> getFieldName eventFieldNames struct (taskId, opcodeId))
                member _.GetEventName struct (providerId, taskId, opcodeId) =
                    lock lck (fun () -> getEventName providers struct (providerId, taskId, opcodeId))
                member _.GetTasks () = lock lck (fun () -> getTasks providers)
                member _.GetOpcodes () = lock lck (fun () -> getOpcodes providers)
                member _.QpcToRelativeTimeInMs qpc =
                    let sessionStartTimeQpc = Interlocked.Read(&sessionStartTimeQpc)
                    let qpcFreq = Interlocked.Read(&qpcFreq)
                    qpcToRelativeTimeInMs sessionStartTimeQpc qpcFreq qpc

                member _.HandleMetadataEvent msg =
                    //lock lck (fun () -> handleMetadataEvent providers eventFieldNames msg)
                    match msg with
                    | EventFieldMetadata (handlerId, fieldId, fieldName) ->
                        eventFieldNames.[(handlerId, fieldId)] <- fieldName
                    | EventProvider (id, name) ->
                        match providers.TryGetValue(id) with
                        | (false, _) -> providers.[id] <- createProvider name
                        | (true, p) ->
                            if p.Name <> name then
                                providers.[id] <- { p with Name = name }

                    | EventTask (providerId, taskId, taskName) ->
                        providers.[providerId].Tasks.[taskId] <- taskName
                    | EventOpcode (providerId, taskId, opcodeId, opcodeName) ->
                        addOpcode providers (providerId, taskId, opcodeId, opcodeName)
                    | SessionConfig (startTimeUtc, startTimeQpc, qpc) ->
                        Interlocked.Exchange(&sessionStartTimeQpc, startTimeQpc) |> ignore
                        Interlocked.Exchange(&qpcFreq, qpc) |> ignore
                        Interlocked.Exchange(&sessionStartTimeUtc, startTimeUtc.Ticks) |> ignore
        }

