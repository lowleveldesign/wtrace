module LowLevelDesign.WTrace.Events.Rpc

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers
open Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsRPC
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.HandlerCommons

type private RpcHandlerState = {
    Broadcast : EventBroadcast
    // a state to keep information about the pending RPC calls
    PendingRpcCalls : DataCache<Guid, TraceEvent * array<struct (string * TraceEventFieldValue)>>
}

[<AutoOpen>]
module private H =

    let toEvent (ev : EtwEvent) eventId eventName activityId path details result =
        {
            EventId = eventId
            TimeStamp = ev.TimeStamp
            ActivityId = activityId
            Duration = TimeSpan.Zero
            ProcessId = ev.ProcessID
            ProcessName = ev.ProcessName
            ThreadId = ev.ThreadID
            EventName = eventName
            EventLevel = int32 ev.Level
            Path = path
            Details = details
            Result = result
        }

    let completeRpcEvent id ts state activityId eventName status =
        match state.PendingRpcCalls.TryGetValue(activityId) with
        | true, (prevEvent, fields) ->
            state.PendingRpcCalls.Remove(activityId) |> ignore
            TraceEventWithFields ({
                prevEvent with
                    EventId = id
                    ActivityId = sprintf "RPC#%s" (activityId.ToString())
                    EventName = eventName
                    Duration = ts - prevEvent.TimeStamp
                    Result = status
            }, fields |> Array.map (toEventField id)) |> state.Broadcast.publishTraceEvent
        | false, _ -> () // this happens sporadically, but I don't think we should worry

    let constructBindingString (endpointName : string) (protSeq : ProtocolSequences) (options : string) =
        let binding =
            match protSeq with
            | ProtocolSequences.LRPC -> $"ncalrpc:[%s{endpointName}"
            | ProtocolSequences.NamedPipes -> $"ncacn_np:[%s{endpointName}"
            | ProtocolSequences.TCP -> $"ncacn_ip_tcp:[%s{endpointName}"
            | ProtocolSequences.RPCHTTP -> $"ncacn_http:[%s{endpointName}"
            | _ -> ""

        if binding <> "" then
            if (not (String.IsNullOrEmpty(options)) && options <> "NULL") then
                binding + $",%s{options}]"
            else
                binding + "]"
        else
            ""
            

    let handleRpcClientCallStart id state (ev : RpcClientCallStartArgs) =
        let binding = constructBindingString ev.Endpoint ev.Protocol ev.Options
        let fields =
            [|
                struct ("Binding", FText binding)
                struct ("InterfaceUuid", FGuid ev.InterfaceUuid)
                struct ("ProcNum", FI32 ev.ProcNum)
            |]

        let path = sprintf "%s (%s) [%d]" (ev.InterfaceUuid.ToString()) binding ev.ProcNum
        let activityId = sprintf "RPC#%s" (ev.ActivityID.ToString())
        let rpcev = (toEvent ev id "RPC/ClientCallStart" activityId path "" WinApi.eventStatusUndefined)
        state.PendingRpcCalls.[ev.ActivityID] <- (rpcev, fields)

        TraceEventWithFields (
            rpcev,
            fields |> Array.map (toEventField id)
        ) |> state.Broadcast.publishTraceEvent

    let handleRpcClientCallStop id state (ev : RpcCallStopArgs) =
        completeRpcEvent id ev.TimeStamp state ev.ActivityID "RPC/ClientCallEnd" ev.Status

    let handleRpcClientCallError id state (ev : RpcClientCallErrorArgs) =
        completeRpcEvent id ev.TimeStamp state ev.ActivityID "RPC/ClientCallError" ev.Status

    let handleRpcServerCallStart id state (ev : RpcServerCallStartArgs) =
        let binding = constructBindingString ev.Endpoint ev.Protocol ev.Options
        let fields =
            [|
                struct ("Binding", FText binding)
                struct ("InterfaceUuid", FGuid ev.InterfaceUuid)
                struct ("ProcNum", FI32 ev.ProcNum)
            |]

        let path = sprintf "%s (%s) [%d]" (ev.InterfaceUuid.ToString()) binding ev.ProcNum
        let activityId = sprintf "RPC#%s" (ev.ActivityID.ToString())
        let rpcev = (toEvent ev id "RPC/ServerCallStart" activityId path "" WinApi.eventStatusUndefined)
        state.PendingRpcCalls.[ev.ActivityID] <- (rpcev, fields)

        TraceEventWithFields (
            rpcev,
            fields |> Array.map (toEventField id)
        ) |> state.Broadcast.publishTraceEvent

    let handleRpcServerCallStop id state (ev : RpcCallStopArgs) =
        completeRpcEvent id ev.TimeStamp state ev.ActivityID "RPC/ServerCallEnd" ev.Status

    let subscribe (source : TraceEventSources, isRundown, idgen, state : obj) =
        let state = state :?> RpcHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        if not isRundown then
            source.Rpc.add_RpcClientCallStart(handleEvent handleRpcClientCallStart)
            source.Rpc.add_RpcClientCallStop(handleEvent handleRpcClientCallStop)
            source.Rpc.add_RpcClientCallError(handleEvent handleRpcClientCallError)
            source.Rpc.add_RpcServerCallStart(handleEvent handleRpcServerCallStart)
            source.Rpc.add_RpcServerCallStop(handleEvent handleRpcServerCallStop)

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
            fun broadcast -> ({
                Broadcast = broadcast
                PendingRpcCalls = DataCache<Guid, TraceEvent * array<struct (string * TraceEventFieldValue)>>(256)
            } :> obj)
        Subscribe = subscribe
    }
