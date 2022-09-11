module LowLevelDesign.WTrace.TraceCounters

open System
open System.Collections.Generic
open LowLevelDesign.WTrace.Processing
open LowLevelDesign.WTrace.Events.FieldValues

type NumericCounter = Dictionary<string, uint64>

type Counters = {
    FileReadBytes : NumericCounter
    FileWrittenBytes : NumericCounter
    TcpReceivedBytes : NumericCounter
    TcpSentBytes : NumericCounter
    UdpReceivedBytes : NumericCounter
    UdpSentBytes : NumericCounter
    RpcClientCalls : Dictionary<string (* binding *) * Guid (* interface uuid *) * int32 (* proc num *), uint64>
    RpcServerCalls : Dictionary<string (* binding *) * Guid (* interface uuid *) * int32 (* proc num *), uint64>
    DpcCalls : Dictionary<uint64, int32 * float>
    IsrCalls : Dictionary<uint64, int32 * float>
}

[<AutoOpen>]
module private H =

    let updateCounter<'T> (counter : Dictionary<'T, uint64>) key count =
        match counter.TryGetValue(key) with
        | (true, n) -> counter.[key] <- n + count
        | (false, _) -> counter.Add(key, count)

    let updateCounterAndElapsedTime (counters : Dictionary<'T, int32 * float>) key count elapsedTime =
        match counters.TryGetValue(key) with
        | (false, _) ->
            counters.Add(key, (count, elapsedTime))
        | (true, (totalCount, totalElapsedTime)) ->
            counters.[key] <- (totalCount + count, totalElapsedTime + elapsedTime)

let init () = 
    {
        FileReadBytes = NumericCounter()
        FileWrittenBytes = NumericCounter()
        TcpReceivedBytes = NumericCounter()
        TcpSentBytes = NumericCounter()
        UdpReceivedBytes = NumericCounter()
        UdpSentBytes = NumericCounter()
        RpcClientCalls = Dictionary<string * Guid * int32, uint64>()
        RpcServerCalls = Dictionary<string * Guid * int32, uint64>()
        DpcCalls = Dictionary<uint64, int32 * float>()
        IsrCalls = Dictionary<uint64, int32 * float>()
    }

let update traceState counters (TraceEventWithFields (ev, fields)) =
    if ev.EventName = "FileIO/Read" then
        updateCounter counters.FileReadBytes ev.Path (getUI64FieldValue fields "ExtraInfo")
    elif ev.EventName = "FileIO/Write" then
        updateCounter counters.FileWrittenBytes ev.Path (getUI64FieldValue fields "ExtraInfo")
    elif ev.EventName = "TcpIp/Recv" then
        updateCounter counters.TcpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
    elif ev.EventName = "TcpIp/Send" then
        updateCounter counters.TcpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
    elif ev.EventName = "TcpIp/RecvIPv6" then
        updateCounter counters.TcpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
    elif ev.EventName = "TcpIp/SendIPv6" then
        updateCounter counters.TcpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
    elif ev.EventName = "UdpIp/Recv" then
        updateCounter counters.UdpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
    elif ev.EventName = "UdpIp/Send" then
        updateCounter counters.UdpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
    elif ev.EventName = "UdpIp/RecvIPv6" then
        updateCounter counters.UdpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
    elif ev.EventName = "UdpIp/SendIPv6" then
        updateCounter counters.UdpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
    elif ev.EventName = "RPC/ServerCallStart" || ev.EventName = "RPC/ClientCallStart" then
        let binding = getTextFieldValue fields "Binding"
        let interfaceUuid = getGuidFieldValue fields "InterfaceUuid"
        let procNum = getI32FieldValue fields "ProcNum"

        let counter = if ev.EventName = "RPC/ServerCallStart" then counters.RpcServerCalls else counters.RpcClientCalls
        updateCounter counter (binding, interfaceUuid, procNum) 1UL
    elif ev.EventName = "PerfInfo/DPC" || ev.EventName = "PerfInfo/ISR" then
        let routine = getUI64FieldValue fields "Routine"
        match (lock traceState (fun () -> SystemImages.findImage traceState routine)) with
        | ValueSome img ->
            let elapsedTime = getF64FieldValue fields "ElapsedTimeMSec"
            let counters = if ev.EventName = "PerfInfo/DPC" then counters.DpcCalls else counters.IsrCalls
            updateCounterAndElapsedTime counters img.BaseAddress 1 elapsedTime
        | ValueNone ->
            Logger.EtwTracing.TraceWarning (sprintf "Possibly missing ImageLoad events. Address: 0x%x" routine)

