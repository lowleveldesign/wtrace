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

    let handleRpcClientCallStart id state (ev : RpcClientCallStartArgs) =
        let fields =
            [|
                struct (nameof ev.Endpoint, FText ev.Endpoint)
                struct (nameof ev.InterfaceUuid, FGuid ev.InterfaceUuid)
                struct (nameof ev.ProcNum, FI32 ev.ProcNum)
                struct (nameof ev.Options, FText ev.Options)
                struct (nameof ev.AuthenticationLevel, FI32 ev.AuthenticationLevel)
                struct (nameof ev.AuthenticationService, FI32 (int32 ev.AuthenticationService))
                struct (nameof ev.ImpersonationLevel, FI32 (int32 ev.ImpersonationLevel))
            |]

        let path = sprintf "%s (%s) [%d]" (ev.InterfaceUuid.ToString()) ev.Endpoint ev.ProcNum
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
        let fields =
            [|
                struct (nameof ev.Endpoint, FText ev.Endpoint)
                struct (nameof ev.InterfaceUuid, FGuid ev.InterfaceUuid)
                struct (nameof ev.ProcNum, FI32 ev.ProcNum)
                struct (nameof ev.Options, FText ev.Options)
                struct (nameof ev.AuthenticationLevel, FI32 ev.AuthenticationLevel)
                struct (nameof ev.AuthenticationService, FI32 (int32 ev.AuthenticationService))
                struct (nameof ev.ImpersonationLevel, FI32 (int32 ev.ImpersonationLevel))
            |]

        let path = sprintf "%s (%s) [%d]" (ev.InterfaceUuid.ToString()) ev.Endpoint ev.ProcNum
        let activityId = sprintf "RPC#%s" (ev.ActivityID.ToString())
        let rpcev = (toEvent ev id "RPC/ServerCallStart" activityId path "" WinApi.eventStatusUndefined)
        state.PendingRpcCalls.[ev.ActivityID] <- (rpcev, fields)

        TraceEventWithFields (
            rpcev,
            fields |> Array.map (toEventField id)
        ) |> state.Broadcast.publishTraceEvent

    let handleRpcServerCallStop id state (ev : RpcCallStopArgs) =
        completeRpcEvent id ev.TimeStamp state ev.ActivityID "RPC/ServerCallEnd" ev.Status

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
                PendingRpcCalls = DataCache<Guid, TraceEvent * array<struct (string * TraceEventFieldValue)>>(256)
            } :> obj)
        Subscribe = subscribe
    }
