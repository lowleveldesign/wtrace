namespace LowLevelDesign.WTrace.Summary

open System
open System.Collections.Generic
open System.Threading
open LowLevelDesign.WTrace

type NumericCounter = Dictionary<string, uint64>

type ImageInMemory = {
    BaseAddress : uint64
    ImageSize : int32
    FileName : string
}

type ProcessRecord = {
    UniqueProcessId : int32
    UniqueParentId : option<int32>
    SystemProcessId : int32
    SystemParentId : int32
    ProcessName : string
    CommandLine : string
    StartTime : DateTime
    ExitTime : DateTime
}

type ProcessNode = { 
    Id : int32
    Children : Dictionary<int32, ProcessNode>
}

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

type TraceSummaryState = {
    // control variables
    DebugSymbols : DebugSymbolSettings
    Cancellation : CancellationToken

    // system images
    LoadedSystemImages : Dictionary<uint64, ImageInMemory>
    SystemImageBaseAddresses : List<uint64>

    // process tree
    Processes : Dictionary<int32, array<ProcessRecord>>
    mutable LastUniqueProcessId : int32

    // rpc
    RpcInterfaceProcedureNames : Dictionary<Guid, array<string>>
    RpcBindingToResolveQueue : Queue<string (* binding *)>
    RpcModulesParsed : HashSet<string (* path *)>

    Counters : Counters
}
