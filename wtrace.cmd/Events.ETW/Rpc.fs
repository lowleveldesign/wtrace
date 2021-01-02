module LowLevelDesign.WTrace.Events.ETW.Rpc

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers
open Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsRPC
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.FieldValues
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.WinApi

type private RpcHandlerState = {
    Broadcast : EventBroadcast
    // a state to keep information about the pending RPC calls
    PendingRpcCalls : DataCache<Guid, TraceEvent * array<struct (string * string)>>
}

type OpcodeId = | ClientCall = 1 | ServerCall = 2

[<AutoOpen>]
module private H =

    let completeRpcEvent id ts state activityId eventName status =
        match state.PendingRpcCalls.TryGetValue(activityId) with
        | true, (prevEvent, fields) ->
            TraceEventWithFields ({
                prevEvent with
                    EventId = id
                    ActivityId = $"RPC#{activityId}"
                    EventName = eventName
                    Duration = ts - prevEvent.TimeStamp
                    Result = status
            }, fields |> Array.map (toEventField id)) |> state.Broadcast.publishTraceEvent
        | false, _ -> () // this happens sporadically, but I don't think we should worry

    let handleRpcClientCallStart id state (ev : RpcClientCallStartArgs) =
        let fields =
            [|
                struct (nameof ev.Endpoint, ev.Endpoint |> s2s)
                struct (nameof ev.InterfaceUuid, ev.InterfaceUuid |> guid2s)
                struct (nameof ev.ProcNum, ev.ProcNum |> i32s)
                struct (nameof ev.Options, ev.Options |> s2s)
                struct (nameof ev.AuthenticationLevel, ev.AuthenticationLevel |> i32s)
                struct (nameof ev.AuthenticationService, int32 ev.AuthenticationService |> i32s)
                struct (nameof ev.ImpersonationLevel, int32 ev.ImpersonationLevel |> i32s)
            |]

        let path = sprintf "%s (%s) [%d]" (ev.InterfaceUuid.ToString()) ev.Endpoint ev.ProcNum
        let rpcev = (toEvent ev id $"RPC#{ev.ActivityID}" path "" eventStatusUndefined)
        state.PendingRpcCalls.[ev.ActivityID] <- (rpcev, fields)

        TraceEventWithFields (
            rpcev,
            fields |> Array.map (toEventField id)
        ) |> state.Broadcast.publishTraceEvent

    let handleRpcClientCallStop id state (ev : RpcCallStopArgs) =
        completeRpcEvent id ev.TimeStamp state ev.ActivityID ev.EventName ev.Status

    let handleRpcClientCallError id state (ev : RpcClientCallErrorArgs) =
        completeRpcEvent id ev.TimeStamp state ev.ActivityID ev.EventName ev.Status

    let handleRpcServerCallStart id state (ev : RpcServerCallStartArgs) =
        let fields =
            [|
                struct (nameof ev.Endpoint, ev.Endpoint |> s2s)
                struct (nameof ev.InterfaceUuid, ev.InterfaceUuid |> guid2s)
                struct (nameof ev.ProcNum, ev.ProcNum |> i32s)
                struct (nameof ev.Options, ev.Options |> s2s)
                struct (nameof ev.AuthenticationLevel, ev.AuthenticationLevel |> i32s)
                struct (nameof ev.AuthenticationService, int32 ev.AuthenticationService |> i32s)
                struct (nameof ev.ImpersonationLevel, int32 ev.ImpersonationLevel |> i32s)
            |] |> Array.map (toEventField id)

        let path = sprintf "%s (%s) [%d]" (ev.InterfaceUuid.ToString()) ev.Endpoint ev.ProcNum
        TraceEventWithFields ((toEvent ev id $"RPC#{ev.ActivityID}" path "" eventStatusUndefined), fields)
        |> state.Broadcast.publishTraceEvent

    let handleRpcServerCallStop id state (ev : RpcCallStopArgs) =
        completeRpcEvent id ev.TimeStamp state ev.ActivityID ev.EventName ev.Status

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
                PendingRpcCalls = DataCache<Guid, TraceEvent * array<struct (string * string)>>(256)
            } :> obj)
        Subscribe = subscribe
    }
