module LowLevelDesign.WTrace.Events.ETW.Rpc

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers
open Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsRPC
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.FieldValues
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.WinApi

#nowarn "44" // disable the deprecation warning as we want to use TimeStampQPC

type private RpcHandlerState = {
    HandlerId : int32
    Broadcast : EventBroadcast
    // a state to keep information about the pending RPC calls
    PendingRpcCalls : DataCache<Guid, TraceEventWithFields>
}

let rpcTaskId = 1

type OpcodeId = | ClientCall = 1 | ServerCall = 2

let rpcProviderId = MicrosoftWindowsRPCTraceEventParser.ProviderGuid

let metadata = [|
    EventProvider (rpcProviderId, "RPC")
    EventTask (rpcProviderId, rpcTaskId, "RPC")
    EventOpcode (rpcProviderId, rpcTaskId, int32 OpcodeId.ClientCall, "ClientCall")
    EventOpcode (rpcProviderId, rpcTaskId, int32 OpcodeId.ServerCall, "ServerCall")
|]

type FieldId =
| Endpoint = 0 | InterfaceUuid = 1 | ProcNum = 2 | Options = 3
| AuthenticationLevel = 4 | AuthenticationService = 5 | ImpersonationLevel = 6

[<AutoOpen>]
module private H =
    let queuePendingEvent id ts state (ev : EtwEvent) path (fields : array<EventFieldDesc>) details = 
        let rpcev = {
            EventId = id
            TimeStamp = Qpc ts
            Duration = Qpc 0L
            ProcessId = ev.ProcessID
            ThreadId = ev.ThreadID
            HandlerId = state.HandlerId
            ProviderId = ev.ProviderGuid
            TaskId = rpcTaskId
            OpcodeId = 0
            EventLevel = int32 ev.Level
            Path = path
            Details = details
            Result = eventStatusUndefined
        }

        state.PendingRpcCalls.[ev.ActivityID] <- TraceEventWithFields (rpcev, fields |> Array.map (toEventField id))

    let completeRpcEvent ts state activityId (opcodeId : OpcodeId) status =
        match state.PendingRpcCalls.TryGetValue(activityId) with
        | true, (TraceEventWithFields (prevEvent, fields)) ->
            state.PendingRpcCalls.Remove(activityId) |> ignore
            TraceEventWithFields ({
                prevEvent with
                    TaskId = rpcTaskId
                    OpcodeId = int32 opcodeId
                    Duration = Qpc (ts - (qpcToInt64 prevEvent.TimeStamp))
                    Result = status
            }, fields) |> state.Broadcast.publishTraceEvent
        | false, _ -> () // this happens sporadically, but I don't think we should worry

    let handleRpcClientCallStart id ts state (ev : RpcClientCallStartArgs) =
        let fields = [|
            struct (int32 FieldId.Endpoint, nameof FieldId.Endpoint, ev.Endpoint |> s2db)
            struct (int32 FieldId.InterfaceUuid, nameof FieldId.InterfaceUuid, ev.InterfaceUuid |> guid2db)
            struct (int32 FieldId.ProcNum, nameof FieldId.ProcNum, ev.ProcNum |> i32db)
            struct (int32 FieldId.Options, nameof FieldId.Options, ev.Options |> s2db)
            struct (int32 FieldId.AuthenticationLevel, nameof FieldId.AuthenticationLevel, ev.AuthenticationLevel |> i32db)
            struct (int32 FieldId.AuthenticationService, nameof FieldId.AuthenticationService, int32 ev.AuthenticationService |> i32db)
            struct (int32 FieldId.ImpersonationLevel, nameof FieldId.ImpersonationLevel, int32 ev.ImpersonationLevel |> i32db) |]

        let path = sprintf "%s (%s) [%d]" (ev.InterfaceUuid.ToString()) ev.Endpoint ev.ProcNum
        queuePendingEvent id ts state ev path fields ""

    let handleRpcClientCallStop ts state (ev : RpcCallStopArgs) =
        completeRpcEvent ts state ev.ActivityID OpcodeId.ClientCall ev.Status

    let handleRpcClientCallError ts state (ev : RpcClientCallErrorArgs) =
        completeRpcEvent ts state ev.ActivityID OpcodeId.ClientCall ev.Status

    let handleRpcServerCallStart id ts state (ev : RpcServerCallStartArgs) =
        let fields = [|
            struct (int32 FieldId.Endpoint, nameof FieldId.Endpoint, ev.Endpoint |> s2db)
            struct (int32 FieldId.InterfaceUuid, nameof FieldId.InterfaceUuid, ev.InterfaceUuid |> guid2db)
            struct (int32 FieldId.ProcNum, nameof FieldId.ProcNum, ev.ProcNum |> i32db)
            struct (int32 FieldId.Options, nameof FieldId.Options, ev.Options |> s2db)
            struct (int32 FieldId.AuthenticationLevel, nameof FieldId.AuthenticationLevel, ev.AuthenticationLevel |> i32db)
            struct (int32 FieldId.AuthenticationService, nameof FieldId.AuthenticationService, int32 ev.AuthenticationService |> i32db)
            struct (int32 FieldId.ImpersonationLevel, nameof FieldId.ImpersonationLevel, int32 ev.ImpersonationLevel |> i32db) |]

        let path = sprintf "%s (%s) [%d]" (ev.InterfaceUuid.ToString()) ev.Endpoint ev.ProcNum
        queuePendingEvent id ts state ev path fields ""

    let handleRpcServerCallStop ts state (ev : RpcCallStopArgs) =
        completeRpcEvent ts state ev.ActivityID OpcodeId.ServerCall ev.Status

    let subscribe (source : TraceEventSource, isRundown, idgen, tsadj, state : obj) =
        let state = state :?> RpcHandlerState
        let handleEvent h = Action<_>(handleEvent idgen tsadj state h)
        let handleEventNoId h = Action<_>(handleEventNoId tsadj state h)
        if isRundown then
            publishHandlerMetadata metadata state.Broadcast.publishMetaEvent
            publishEventFieldsMetadata<FieldId> state.HandlerId state.Broadcast.publishMetaEvent
        else
            let parser = MicrosoftWindowsRPCTraceEventParser(source)

            parser.add_RpcClientCallStart(handleEvent handleRpcClientCallStart)
            parser.add_RpcClientCallStop(handleEventNoId handleRpcClientCallStop)
            parser.add_RpcClientCallError(handleEventNoId handleRpcClientCallError)
            parser.add_RpcServerCallStart(handleEvent handleRpcServerCallStart)
            parser.add_RpcServerCallStop(handleEventNoId handleRpcServerCallStop)

let createEtwHandler () =
    {
        KernelFlags = NtKeywords.None
        KernelStackFlags = NtKeywords.None
        KernelRundownFlags = NtKeywords.None
        Providers = Array.singleton {
                                        Id = MicrosoftWindowsRPCTraceEventParser.ProviderGuid
                                        Name = "Microsoft-Windows-RPC"
                                        Level = TraceEventLevel.Verbose
                                        Keywords = UInt64.MaxValue
                                        RundownLevel = TraceEventLevel.Verbose
                                        RundownKeywords = 0UL
                                    }
        Initialize = 
            fun (id, broadcast) -> ({
                HandlerId = id
                Broadcast = broadcast
                PendingRpcCalls = DataCache<Guid, TraceEventWithFields>(256)
            } :> obj)
        Subscribe = subscribe
    }
