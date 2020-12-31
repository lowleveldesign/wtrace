module LowLevelDesign.WTrace.Events.ETW.FileIO

open FSharp.Collections
open System
open System.Collections.Generic
open System.IO
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open Microsoft.Diagnostics.Tracing
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.FieldValues
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.WinApi

type private FileIoHandlerState = {
    HandlerId : int32
    Broadcast : EventBroadcast
    // a state to keep information about the pending IO requests
    PendingFileIo : DataCache<uint64, TraceEventWithFields>
    // we need to keep this map in our handler. KernelTraceEventParser has it too,
    // but it removes the mapping too quickly (Cleanup, for example)
    FileIdToName : Dictionary<uint64, string>
}

let metadata = [|
    EventProvider (kernelProviderId, "Kernel")
    EventTask (kernelProviderId, 6, "FileIO")
    EventOpcode (kernelProviderId, 6, 0, "Name")
    EventOpcode (kernelProviderId, 6, 32, "FileCreate")
    EventOpcode (kernelProviderId, 6, 35, "FileDelete")
    EventOpcode (kernelProviderId, 6, 36, "FileRundown")
    EventOpcode (kernelProviderId, 6, 37, "MapFile")
    EventOpcode (kernelProviderId, 6, 38, "UnmapFile")
    EventOpcode (kernelProviderId, 6, 64, "Create")
    EventOpcode (kernelProviderId, 6, 65, "Cleanup")
    EventOpcode (kernelProviderId, 6, 66, "Close")
    EventOpcode (kernelProviderId, 6, 67, "Read")
    EventOpcode (kernelProviderId, 6, 68, "Write")
    EventOpcode (kernelProviderId, 6, 69, "SetInfo")
    EventOpcode (kernelProviderId, 6, 70, "Delete")
    EventOpcode (kernelProviderId, 6, 71, "Rename")
    EventOpcode (kernelProviderId, 6, 72, "DirEnum")
    EventOpcode (kernelProviderId, 6, 73, "Flush")
    EventOpcode (kernelProviderId, 6, 74, "QueryInfo")
    EventOpcode (kernelProviderId, 6, 75, "FSControl")
    EventOpcode (kernelProviderId, 6, 76, "OperationEnd")
    EventOpcode (kernelProviderId, 6, 77, "DirNotify")
|]

type FieldId =
| CreateDisposition = 0 | CreateOptions = 1 | FileAttributes = 2
| ShareAccess = 3 | FileName = 4 | FileIndex = 5 | Length = 6
| InfoClass = 7 | Offset = 8 | IoSize = 9 | IoFlags = 10 | ExtraInfo = 12

#nowarn "44" // disable the deprecation warning as we want to use TimeStampQPC

[<AutoOpen>]
module private H =
    let queuePendingEvent id ts state (ev : EtwEvent) irpPtr fileName (fields : array<EventFieldDesc>) details = 
        let ev = toEvent state.HandlerId ev id ts fileName details eventStatusUndefined
        state.PendingFileIo.[irpPtr] <- TraceEventWithFields (ev, fields |> Array.map (toEventField id))

    let fileShareStr (fs : FileShare) =
        if (fs &&& FileShare.ReadWrite &&& FileShare.Delete <> FileShare.None) then "rwd"
        elif (fs &&& FileShare.ReadWrite <> FileShare.None) then "rw-"
        elif (fs &&& FileShare.Read <> FileShare.None) then "r--"
        elif (fs &&& FileShare.Write <> FileShare.None) then "-w-"
        else "---"

    let evictFileIds state fileIds =
        fileIds |> Array.iter (state.FileIdToName.Remove >> ignore)

    let getFileNameByFileIds state struct (fileKey, fileObject) =
        match (state.FileIdToName.TryGetValue(fileObject)) with
        | (true, name) -> name
        | (false, _) -> match (state.FileIdToName.TryGetValue(fileKey)) with
                        | (true, name) -> name
                        | (false, _) -> sprintf "<0x%X>" fileObject

    let mapFileNameToFileId state fileId fileName =
        state.FileIdToName.[fileId] <- fileName

    let handleFileIoCreate id ts state (ev : FileIOCreateTraceData) =
        mapFileNameToFileId state ev.FileObject ev.FileName
        let createDisposition = sprintf "%A" ev.CreateDisposition
        let createOptions = sprintf "%A" ev.CreateOptions 
        let fields = [|
            struct (int32 FieldId.CreateDisposition, nameof FieldId.CreateDisposition, createDisposition |> s2db)
            struct (int32 FieldId.CreateOptions, nameof FieldId.CreateOptions, createOptions |> s2db)
            struct (int32 FieldId.FileAttributes, nameof FieldId.FileAttributes, sprintf "%A" ev.FileAttributes |> s2db)
            struct (int32 FieldId.ShareAccess, nameof FieldId.ShareAccess, fileShareStr ev.ShareAccess |> s2db) |]

        let details = sprintf "disposition: %s; options: %s" createDisposition createOptions
        queuePendingEvent id ts state ev ev.IrpPtr ev.FileName fields details

    let handleFileIoDirEnum id ts state (ev : FileIODirEnumTraceData) =
        let fields = [|
            struct (int32 FieldId.FileName, nameof FieldId.FileName, ev.FileName |> s2db)
            struct (int32 FieldId.FileIndex, nameof FieldId.FileIndex, ev.FileIndex |> i32db)
            struct (int32 FieldId.Length, nameof FieldId.Length, ev.Length |> i32db) |]

        let details = sprintf "name: %s; index: %d; length: %d" ev.FileName ev.FileIndex ev.Length
        queuePendingEvent id ts state ev ev.IrpPtr ev.DirectoryName fields details

    let handleFileIoInfo id ts state (ev : FileIOInfoTraceData) =
        let fields = Array.singleton (struct (int32 FieldId.InfoClass, nameof FieldId.InfoClass, 
                                              ev.InfoClass |> i32db))

        let fileName = struct (ev.FileKey, ev.FileObject) |> getFileNameByFileIds state
        let details = sprintf "class info: %d" ev.InfoClass
        queuePendingEvent id ts state ev ev.IrpPtr fileName fields details

    let handleFileIoReadWrite id ts state (ev : FileIOReadWriteTraceData) =
        let fields = [|
            struct (int32 FieldId.Offset, nameof FieldId.Offset, ev.Offset |> i64db)
            struct (int32 FieldId.IoSize, nameof FieldId.IoSize, ev.IoSize |> i32db)
            struct (int32 FieldId.IoFlags, nameof FieldId.IoFlags, ev.IoFlags |> i32db) |]

        let fileName = struct (ev.FileKey, ev.FileObject) |> getFileNameByFileIds state
        let details = sprintf "offset: %d; size: %d" ev.Offset ev.IoSize
        queuePendingEvent id ts state ev ev.IrpPtr fileName fields details

    let handleFileIoSimpleOp id ts state (ev : FileIOSimpleOpTraceData) =
        let fileName = struct (ev.FileKey, ev.FileObject) |> getFileNameByFileIds state
        queuePendingEvent id ts state ev ev.IrpPtr fileName Array.empty<EventFieldDesc> ""

        if int32(ev.Opcode) = 66 (* close *) then
            [| ev.FileKey; ev.FileObject |] |> evictFileIds state

    let handleFileIoOpEnd ts state (ev : FileIOOpEndTraceData) =
        let completeWTraceEvent (TraceEventWithFields (ev, fields)) (completion : FileIOOpEndTraceData) =
            let ev = { ev with Duration = Qpc (ts - (qpcToInt64 ev.TimeStamp))
                               Result = completion.NtStatus }
            let field = struct (int32 FieldId.ExtraInfo, nameof FieldId.ExtraInfo, completion.ExtraInfo |> ui64db)
                        |> toEventField ev.EventId
            TraceEventWithFields (ev, [| field |] |> Array.append fields)

        let irp = ev.IrpPtr
        match state.PendingFileIo.TryGetValue(irp) with
        | true, prevEvent ->
            state.PendingFileIo.Remove(irp) |> ignore
            state.Broadcast.publishTraceEvent (completeWTraceEvent prevEvent ev)
        | false, _ -> () // FIXME: this happens sporadically - I'm not yet sure why

    let handleFileIoName state (ev : FileIONameTraceData) =
        let opcode = int32 ev.Opcode
        if opcode = 0 (* name *) || opcode = 36 (* rundown *) then
            mapFileNameToFileId state ev.FileKey ev.FileName

    let subscribe (source : TraceEventSource, isRundown, idgen, tsadj, state : obj) =
        let state = state :?> FileIoHandlerState
        let handleEvent h = Action<_>(handleEvent idgen tsadj state h)
        let handleEventNoId h = Action<_>(handleEventNoId tsadj state h)
        let handle h = Action<_>(h state)
        if isRundown then
            source.Kernel.add_FileIOName(handle handleFileIoName)
            source.Kernel.add_FileIOFileRundown(handle handleFileIoName)

            publishHandlerMetadata metadata state.Broadcast.publishMetaEvent
            publishEventFieldsMetadata<FieldId> state.HandlerId state.Broadcast.publishMetaEvent
        else
            source.Kernel.add_FileIOCreate(handleEvent handleFileIoCreate)
            source.Kernel.add_FileIOCleanup(handleEvent handleFileIoSimpleOp)
            source.Kernel.add_FileIOClose(handleEvent handleFileIoSimpleOp)
            source.Kernel.add_FileIOFlush(handleEvent handleFileIoSimpleOp)
            source.Kernel.add_FileIODelete(handleEvent handleFileIoInfo)
            source.Kernel.add_FileIOFSControl(handleEvent handleFileIoInfo)
            source.Kernel.add_FileIOQueryInfo(handleEvent handleFileIoInfo)
            source.Kernel.add_FileIOSetInfo(handleEvent handleFileIoInfo)
            source.Kernel.add_FileIORename(handleEvent handleFileIoInfo)
            source.Kernel.add_FileIORead(handleEvent handleFileIoReadWrite)
            source.Kernel.add_FileIOWrite(handleEvent handleFileIoReadWrite)
            source.Kernel.add_FileIOOperationEnd(handleEventNoId handleFileIoOpEnd)
            source.Kernel.add_FileIODirEnum(handleEvent handleFileIoDirEnum)


let createEtwHandler () =
    {
        KernelFlags = NtKeywords.FileIOInit ||| NtKeywords.FileIO
        KernelStackFlags = NtKeywords.FileIO
        KernelRundownFlags = NtKeywords.DiskFileIO ||| NtKeywords.DiskIO
        Providers = Array.empty<EtwEventProvider>
        Initialize = 
            fun (id, broadcast) -> ({
                HandlerId = id
                Broadcast = broadcast
                PendingFileIo = DataCache<uint64, TraceEventWithFields>(256)
                FileIdToName = Dictionary<uint64, string>()
            } :> obj)
        Subscribe = subscribe
    }
