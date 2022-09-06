module LowLevelDesign.WTrace.Events.TcpIp

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.HandlerCommons

type private TcpIpHandlerState = {
    Broadcast : EventBroadcast
}

[<AutoOpen>]
module private H =

    let handleTcpIpConnect id state (ev : TcpIpConnectTraceData) =
        let fields = [|
            struct (nameof ev.connid, FUI64 ev.connid)
            struct (nameof ev.seqnum, FI32 ev.seqnum)
            struct (nameof ev.mss, FI32 ev.mss)
            struct (nameof ev.size, FI32 ev.size) |]

        let details = sprintf "conn: %d, seq: %d" ev.connid ev.seqnum
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let activityId = sprintf "conn#%d" ev.connid
        let ev = toEvent ev id activityId path details WinApi.eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleTcpIp6Connect id state (ev : TcpIpV6ConnectTraceData) =
        let fields = [|
            struct (nameof ev.connid, FUI64 ev.connid)
            struct (nameof ev.seqnum, FI32 ev.seqnum)
            struct (nameof ev.mss, FI32 ev.mss)
            struct (nameof ev.size, FI32 ev.size) |]

        let details = sprintf "conn: %d, seq: %d" ev.connid ev.seqnum
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let activityId = sprintf "conn#%d" ev.connid
        let ev = toEvent ev id activityId path details WinApi.eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleTcpIpData id state (ev : TcpIpTraceData) =
        let fields = [|
            struct (nameof ev.connid, FUI64 ev.connid)
            struct (nameof ev.seqnum, FI32 ev.seqnum)
            struct (nameof ev.size, FI32 ev.size) |]

        let details = sprintf "conn: %d, seq: %d, size: %d" ev.connid ev.seqnum ev.size
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let activityId = sprintf "conn#%d" ev.connid
        let ev = toEvent ev id activityId path details WinApi.eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleTcpIp6Data id state (ev : TcpIpV6TraceData) =
        let fields = [|
            struct (nameof ev.connid, FUI64 ev.connid)
            struct (nameof ev.seqnum, FI32 ev.seqnum)
            struct (nameof ev.size, FI32 ev.size) |]

        let details = sprintf "conn: %d, seq: %d, size: %d" ev.connid ev.seqnum ev.size
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let activityId = sprintf "conn#%d" ev.connid
        let ev = toEvent ev id activityId path details WinApi.eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleTcpIpSend id state (ev : TcpIpSendTraceData) =
        let fields = [|
            struct (nameof ev.connid, FUI64 ev.connid)
            struct (nameof ev.seqnum, FI32 ev.seqnum)
            struct (nameof ev.size, FI32 ev.size) |]

        let details = sprintf "conn: %d, seq: %d, size: %d" ev.connid ev.seqnum ev.size
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let activityId = sprintf "conn#%d" ev.connid
        let ev = toEvent ev id activityId path details WinApi.eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleTcpIp6Send id state (ev : TcpIpV6SendTraceData) =
        let fields = [|
            struct (nameof ev.connid, FUI64 ev.connid)
            struct (nameof ev.seqnum, FI32 ev.seqnum)
            struct (nameof ev.size, FI32 ev.size) |]

        let details = sprintf "conn: %d, seq: %d, size: %d" ev.connid ev.seqnum ev.size
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let activityId = sprintf "conn#%d" ev.connid
        let ev = toEvent ev id activityId path details WinApi.eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let subscribe (source : TraceEventSources, isRundown, idgen, state : obj) =
        let state = state :?> TcpIpHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        if not isRundown then
            source.Kernel.add_TcpIpAccept(handleEvent handleTcpIpConnect)
            source.Kernel.add_TcpIpAcceptIPV6(handleEvent handleTcpIp6Connect)
            source.Kernel.add_TcpIpARPCopy(handleEvent handleTcpIpData)
            source.Kernel.add_TcpIpConnect(handleEvent handleTcpIpConnect)
            source.Kernel.add_TcpIpConnectIPV6(handleEvent handleTcpIp6Connect)
            source.Kernel.add_TcpIpDisconnect(handleEvent handleTcpIpData)
            source.Kernel.add_TcpIpDisconnectIPV6(handleEvent handleTcpIp6Data)
            source.Kernel.add_TcpIpDupACK(handleEvent handleTcpIpData)
            source.Kernel.add_TcpIpFullACK(handleEvent handleTcpIpData)
            source.Kernel.add_TcpIpPartACK(handleEvent handleTcpIpData)
            source.Kernel.add_TcpIpReconnect(handleEvent handleTcpIpData)
            source.Kernel.add_TcpIpReconnectIPV6(handleEvent handleTcpIp6Data)
            source.Kernel.add_TcpIpTCPCopy(handleEvent handleTcpIpData)
            source.Kernel.add_TcpIpTCPCopyIPV6(handleEvent handleTcpIp6Data)
            source.Kernel.add_TcpIpRetransmit(handleEvent handleTcpIpData)
            source.Kernel.add_TcpIpRetransmitIPV6(handleEvent handleTcpIp6Data)
            source.Kernel.add_TcpIpRecv(handleEvent handleTcpIpData)
            source.Kernel.add_TcpIpRecvIPV6(handleEvent handleTcpIp6Data)
            source.Kernel.add_TcpIpSend(handleEvent handleTcpIpSend)
            source.Kernel.add_TcpIpSendIPV6(handleEvent handleTcpIp6Send)


let createEtwHandler () =
    {
        KernelFlags = NtKeywords.NetworkTCPIP
        KernelStackFlags = NtKeywords.NetworkTCPIP
        KernelRundownFlags = NtKeywords.None
        Providers = Array.empty<EtwEventProvider>
        Initialize = 
            fun broadcast -> ({
                Broadcast = broadcast
            } :> obj)
        Subscribe = subscribe
    }

