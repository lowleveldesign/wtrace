namespace LowLevelDesign.WTrace.Processing

open System
open System.Collections.Generic

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

type LiveTraceState = {

    // system images
    LoadedSystemImages : Dictionary<uint64, ImageInMemory>
    SystemImageBaseAddresses : List<uint64>

    // process tree
    Processes : Dictionary<int32, array<ProcessRecord>>
    LoadedProcessModules : Dictionary<int32, HashSet<string>>
    mutable LastUniqueProcessId : int32

    // rpc
    RpcInterfaceProcedureNames : Dictionary<Guid, array<string>>
    RpcBindingToResolveQueue : Queue<string (* binding *)>
    RpcModulesParsed : HashSet<string (* path *)>
}
