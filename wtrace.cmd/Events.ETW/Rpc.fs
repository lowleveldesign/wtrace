module LowLevelDesign.WTrace.Events.ETW.Rpc

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers
open Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsRPC
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.FieldValues
open LowLevelDesign.WTrace

type private RpcHandlerState = {
    Broadcast : EventBroadcast
}

type OpcodeId = | ClientCall = 1 | ServerCall = 2

[<AutoOpen>]
module private H =
    let queuePendingEvent id state (ev : EtwEvent) path fields details = 
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

    let completeRpcEvent state activityId (opcodeId : OpcodeId) status =
        match state.PendingRpcCalls.TryGetValue(activityId) with
        | true, (TraceEventWithFields (prevEvent, fields)) ->
            TraceEventWithFields ({
                prevEvent with
                    TaskId = rpcTaskId
                    OpcodeId = int32 opcodeId
                    Duration = Qpc (ts - (qpcToInt64 prevEvent.TimeStamp))
                    Result = status
            }, fields) |> state.Broadcast.publishTraceEvent
        | false, _ -> () // this happens sporadically, but I don't think we should worry

    let handleRpcClientCallStart id state (ev : RpcClientCallStartArgs) =
        let fields = [|
            struct (nameof ev.Endpoint, ev.Endpoint |> s2db)
            struct (nameof ev.InterfaceUuid, ev.InterfaceUuid |> guid2db)
            struct (nameof ev.ProcNum, ev.ProcNum |> i32db)
            struct (nameof ev.Options, ev.Options |> s2db)
            struct (nameof ev.AuthenticationLevel, ev.AuthenticationLevel |> i32db)
            struct (nameof ev.AuthenticationService, int32 ev.AuthenticationService |> i32db)
            struct (nameof ev.ImpersonationLevel, int32 ev.ImpersonationLevel |> i32db) |]

        let path = sprintf "%s (%s) [%d]" (ev.InterfaceUuid.ToString()) ev.Endpoint ev.ProcNum
        TraceEventWithFields ({
            prevEvent with
                TaskId = rpcTaskId
                OpcodeId = int32 opcodeId
                Duration = Qpc (ts - (qpcToInt64 prevEvent.TimeStamp))
                Result = status
        }, fields) |> state.Broadcast.publishTraceEvent

        queuePendingEvent id ts state ev path fields ""

    let handleRpcClientCallStop ts state (ev : RpcCallStopArgs) =
        completeRpcEvent ts state ev.ActivityID OpcodeId.ClientCall ev.Status

    let handleRpcClientCallError ts state (ev : RpcClientCallErrorArgs) =
        completeRpcEvent ts state ev.ActivityID OpcodeId.ClientCall ev.Status

    let handleRpcServerCallStart id state (ev : RpcServerCallStartArgs) =
        let fields = [|
            struct (nameof ev.Endpoint, ev.Endpoint |> s2db)
            struct (nameof ev.InterfaceUuid, ev.InterfaceUuid |> guid2db)
            struct (nameof ev.ProcNum, ev.ProcNum |> i32db)
            struct (nameof ev.Options, ev.Options |> s2db)
            struct (nameof ev.AuthenticationLevel, ev.AuthenticationLevel |> i32db)
            struct (nameof ev.AuthenticationService, int32 ev.AuthenticationService |> i32db)
            struct (nameof ev.ImpersonationLevel, int32 ev.ImpersonationLevel |> i32db) |]

        let path = sprintf "%s (%s) [%d]" (ev.InterfaceUuid.ToString()) ev.Endpoint ev.ProcNum
        queuePendingEvent id state ev path fields ""

    let handleRpcServerCallStop ts state (ev : RpcCallStopArgs) =
        completeRpcEvent ts state ev.ActivityID OpcodeId.ServerCall ev.Status

    let subscribe (source : TraceEventSource, isRundown, idgen, state : obj) =
        let state = state :?> RpcHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        if not isRundown then
            let parser = MicrosoftWindowsRPCTraceEventParser(source)

            parser.add_RpcClientCallStart(handleEvent handleRpcClientCallStart)
            parser.add_RpcClientCallStop(handleEvent handleRpcClientCallStop)
            parser.add_RpcClientCallError(handleEvent handleRpcClientCallError)
            parser.add_RpcServerCallStart(handleEvent handleRpcServerCallStart)
            parser.add_RpcServerCallStop(handleEvent handleRpcServerCallStop)

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
            fun (broadcast) -> ({
                Broadcast = broadcast
            } :> obj)
        Subscribe = subscribe
    }
