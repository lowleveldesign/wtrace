module LowLevelDesign.WTrace.Events.UdpIp

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.HandlerCommons

type private UdpIpHandlerState = {
    Broadcast : EventBroadcast
}

[<AutoOpen>]
module private H =

    let noFields = Array.empty<TraceEventField>
    
    let handleUdpIpFail id state (ev : UdpIpFailTraceData) =
        let ev = toEvent ev id "" "" "" ev.FailureCode
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, noFields))

    let handleUdpIpData id state (ev : UdpIpTraceData) =
        let fields = [|
            struct (nameof ev.size, FI32 ev.size) |]

        let details = sprintf "size: %d" ev.size
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let ev = toEvent ev id "" path details WinApi.eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleUdpIp6Data id state (ev : UpdIpV6TraceData) =
        let fields = [|
            struct (nameof ev.size, FI32 ev.size) |]

        let details = sprintf "size: %d" ev.size
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let activityId = sprintf "conn#%d" ev.connid
        let ev = toEvent ev id activityId path details WinApi.eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let subscribe (source : TraceEventSource, isRundown, idgen, state : obj) =
        let state = state :?> UdpIpHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        if not isRundown then
            source.Kernel.add_UdpIpFail(handleEvent handleUdpIpFail)
            source.Kernel.add_UdpIpRecv(handleEvent handleUdpIpData)
            source.Kernel.add_UdpIpRecvIPV6(handleEvent handleUdpIp6Data)
            source.Kernel.add_UdpIpSend(handleEvent handleUdpIpData)
            source.Kernel.add_UdpIpSendIPV6(handleEvent handleUdpIp6Data)


let createEtwHandler () =
    {
        KernelFlags = NtKeywords.NetworkTCPIP
        KernelStackFlags = NtKeywords.NetworkTCPIP
        KernelRundownFlags = NtKeywords.None
        Providers = Array.empty<EtwEventProvider>
        Initialize = 
            fun (broadcast) -> ({
                Broadcast = broadcast
            } :> obj)
        Subscribe = subscribe
    }

