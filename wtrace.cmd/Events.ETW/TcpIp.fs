module LowLevelDesign.WTrace.Events.ETW.TcpIp

open System
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace.WinApi
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.FieldValues
open LowLevelDesign.WTrace
open Microsoft.Diagnostics.Tracing

type private TcpIpHandlerState = {
    HandlerId : int32
    Broadcast : EventBroadcast
}

type FieldId = | ConnId = 0 | SeqNum = 1 | MSS = 2 | Size = 3

let metadata = [|
    EventProvider (kernelProviderId, "Kernel")
    EventTask (kernelProviderId, 7, "TcpIp")
    EventOpcode (kernelProviderId, 7, 10, "Send")
    EventOpcode (kernelProviderId, 7, 11, "Recv")
    EventOpcode (kernelProviderId, 7, 12, "Connect")
    EventOpcode (kernelProviderId, 7, 13, "Disconnect")
    EventOpcode (kernelProviderId, 7, 14, "Retransmit")
    EventOpcode (kernelProviderId, 7, 15, "Accept")
    EventOpcode (kernelProviderId, 7, 16, "Reconnect")
    EventOpcode (kernelProviderId, 7, 17, "Fail")
    EventOpcode (kernelProviderId, 7, 18, "TCPCopy")
    EventOpcode (kernelProviderId, 7, 19, "ARPCopy")
    EventOpcode (kernelProviderId, 7, 20, "FullACK")
    EventOpcode (kernelProviderId, 7, 21, "PartACK")
    EventOpcode (kernelProviderId, 7, 22, "DupACK")
    EventOpcode (kernelProviderId, 7, 26, "SendIPv6")
    EventOpcode (kernelProviderId, 7, 27, "RecvIPv6")
    EventOpcode (kernelProviderId, 7, 29, "DisconnectIPv6")
    EventOpcode (kernelProviderId, 7, 30, "RetransmitIPv6")
    EventOpcode (kernelProviderId, 7, 32, "ReconnectIPv6")
    EventOpcode (kernelProviderId, 7, 34, "TCPCopyIPv6")
    EventOpcode (kernelProviderId, 7, 28, "ConnectIPv6")
    EventOpcode (kernelProviderId, 7, 31, "AcceptIPv6")
|]

[<AutoOpen>]
module private H =

    let handleTcpIpConnect id ts state (ev : TcpIpConnectTraceData) =
        let fields = [|
            struct (int32 FieldId.ConnId, nameof FieldId.ConnId, ev.connid |> ui64db)
            struct (int32 FieldId.SeqNum, nameof FieldId.SeqNum, ev.seqnum |> i32db)
            struct (int32 FieldId.MSS, nameof FieldId.MSS, ev.mss |> i32db)
            struct (int32 FieldId.Size, nameof FieldId.Size, ev.size |> i32db) |]

        let details = sprintf "conn: %d; seq: %d" ev.connid ev.seqnum
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let ev = toEvent state.HandlerId ev id ts path details eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleTcpIp6Connect id ts state (ev : TcpIpV6ConnectTraceData) =
        let fields = [|
            struct (int32 FieldId.ConnId, nameof FieldId.ConnId, ev.connid |> ui64db)
            struct (int32 FieldId.SeqNum, nameof FieldId.SeqNum, ev.seqnum |> i32db)
            struct (int32 FieldId.MSS, nameof FieldId.MSS, ev.mss |> i32db)
            struct (int32 FieldId.Size, nameof FieldId.Size, ev.size |> i32db) |]

        let details = sprintf "conn: %d; seq: %d" ev.connid ev.seqnum
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let ev = toEvent state.HandlerId ev id ts path details eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleTcpIpData id ts state (ev : TcpIpTraceData) =
        let fields = [|
            struct (int32 FieldId.ConnId, nameof FieldId.ConnId, ev.connid |> ui64db)
            struct (int32 FieldId.SeqNum, nameof FieldId.SeqNum, ev.seqnum |> i32db)
            struct (int32 FieldId.Size, nameof FieldId.Size, ev.size |> i32db) |]

        let details = sprintf "conn: %d; seq: %d; size: %d" ev.connid ev.seqnum ev.size
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let ev = toEvent state.HandlerId ev id ts path details eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleTcpIp6Data id ts state (ev : TcpIpV6TraceData) =
        let fields = [|
            struct (int32 FieldId.ConnId, nameof FieldId.ConnId, ev.connid |> ui64db)
            struct (int32 FieldId.SeqNum, nameof FieldId.SeqNum, ev.seqnum |> i32db)
            struct (int32 FieldId.Size, nameof FieldId.Size, ev.size |> i32db) |]

        let details = sprintf "conn: %d; seq: %d; size: %d" ev.connid ev.seqnum ev.size
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let ev = toEvent state.HandlerId ev id ts path details eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleTcpIpSend id ts state (ev : TcpIpSendTraceData) =
        let fields = [|
            struct (int32 FieldId.ConnId, nameof FieldId.ConnId, ev.connid |> ui64db)
            struct (int32 FieldId.SeqNum, nameof FieldId.SeqNum, ev.seqnum |> i32db)
            struct (int32 FieldId.Size, nameof FieldId.Size, ev.size |> i32db) |]

        let details = sprintf "conn: %d; seq: %d; size: %d" ev.connid ev.seqnum ev.size
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let ev = toEvent state.HandlerId ev id ts path details eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let handleTcpIp6Send id ts state (ev : TcpIpV6SendTraceData) =
        let fields = [|
                struct (int32 FieldId.ConnId, nameof FieldId.ConnId, ev.connid |> ui64db)
                struct (int32 FieldId.SeqNum, nameof FieldId.SeqNum, ev.seqnum |> i32db)
                struct (int32 FieldId.Size, nameof FieldId.Size, ev.size |> i32db) |]

        let details = sprintf "conn: %d; seq: %d; size: %d" ev.connid ev.seqnum ev.size
        let path = sprintf "%s:%d -> %s:%d" (ev.saddr.ToString()) ev.sport (ev.daddr.ToString()) ev.dport
        let ev = toEvent state.HandlerId ev id ts path details eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields |> Array.map (toEventField id)))

    let subscribe (source : TraceEventSource, isRundown, idgen, tsadj, state : obj) =
        let state = state :?> TcpIpHandlerState
        let handleEvent h = Action<_>(handleEvent idgen tsadj state h)
        if isRundown then
            publishHandlerMetadata metadata state.Broadcast.publishMetaEvent
            publishEventFieldsMetadata<FieldId> state.HandlerId state.Broadcast.publishMetaEvent
        else
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
            fun (id, broadcast) -> ({
                HandlerId = id
                Broadcast = broadcast
            } :> obj)
        Subscribe = subscribe
    }

