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
    Broadcast : EventBroadcast
    // a state to keep information about the pending IO requests
    PendingFileIo : DataCache<uint64, TraceEvent * array<struct (string * string)>>
    // we need to keep this map in our handler. KernelTraceEventParser has it too,
    // but it removes the mapping too quickly (Cleanup, for example)
    FileIdToName : Dictionary<uint64, string>
}

[<AutoOpen>]
module private H =
    let queuePendingEvent state (ev : EtwEvent) irpPtr fileId fileName fields details = 
        let ev = toEvent ev 0 $"File#%d{fileId}" fileName details eventStatusUndefined
        state.PendingFileIo.[irpPtr] <- (ev, fields)

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

    let handleFileIoCreate state (ev : FileIOCreateTraceData) =
        mapFileNameToFileId state ev.FileObject ev.FileName
        let createDisposition = sprintf "%A" ev.CreateDisposition
        let createOptions = sprintf "%A" ev.CreateOptions 
        let fields = [|
            struct (nameof ev.CreateDisposition, createDisposition |> s2s)
            struct (nameof ev.CreateOptions, createOptions |> s2s)
            struct (nameof ev.FileAttributes, sprintf "%A" ev.FileAttributes |> s2s)
            struct (nameof ev.ShareAccess, fileShareStr ev.ShareAccess |> s2s) |]

        let details = sprintf "disposition: %s; options: %s" createDisposition createOptions
        queuePendingEvent state ev ev.IrpPtr ev.FileObject ev.FileName fields details

    let handleFileIoDirEnum state (ev : FileIODirEnumTraceData) =
        let fields = [|
            struct (nameof ev.FileName, ev.FileName |> s2s)
            struct (nameof ev.FileIndex, ev.FileIndex |> i32s)
            struct (nameof ev.Length, ev.Length |> i32s) |]

        let details = sprintf "name: %s; index: %d; length: %d" ev.FileName ev.FileIndex ev.Length
        queuePendingEvent state ev ev.IrpPtr ev.FileObject ev.DirectoryName fields details

    let handleFileIoInfo state (ev : FileIOInfoTraceData) =
        let fields = Array.singleton (struct (nameof ev.InfoClass, ev.InfoClass |> i32s))

        let fileName = struct (ev.FileKey, ev.FileObject) |> getFileNameByFileIds state
        let details = sprintf "class info: %d" ev.InfoClass
        queuePendingEvent state ev ev.IrpPtr ev.FileObject fileName fields details

    let handleFileIoReadWrite state (ev : FileIOReadWriteTraceData) =
        let fields = [|
            struct (nameof ev.Offset, ev.Offset |> i64s)
            struct (nameof ev.IoSize, ev.IoSize |> i32s)
            struct (nameof ev.IoFlags, ev.IoFlags |> i32s) |]

        let fileName = struct (ev.FileKey, ev.FileObject) |> getFileNameByFileIds state
        let details = sprintf "offset: %d; size: %d" ev.Offset ev.IoSize
        queuePendingEvent state ev ev.IrpPtr ev.FileObject fileName fields details

    let handleFileIoSimpleOp state (ev : FileIOSimpleOpTraceData) =
        let fileName = struct (ev.FileKey, ev.FileObject) |> getFileNameByFileIds state
        queuePendingEvent state ev ev.IrpPtr ev.FileObject fileName [| |] ""

        if int32(ev.Opcode) = 66 (* close *) then
            [| ev.FileKey; ev.FileObject |] |> evictFileIds state

    let handleFileIoOpEnd id state (ev : FileIOOpEndTraceData) =
        let completeWTraceEvent (ev, fields) (completion : FileIOOpEndTraceData) =
            let ev = { ev with EventId = id
                               Duration = completion.TimeStamp - ev.TimeStamp
                               Result = completion.NtStatus }
            let fields =
                fields
                |> Array.append [| struct (nameof completion.ExtraInfo, completion.ExtraInfo |> ui64s) |]
                |> Array.map (toEventField id)
            TraceEventWithFields (ev, fields)

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

    let subscribe (source : TraceEventSource, isRundown, idgen, state : obj) =
        let state = state :?> FileIoHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        let handleEventNoId h = Action<_>(handleEventNoId state h)
        let handle h = Action<_>(h state)
        if isRundown then
            source.Kernel.add_FileIOName(handle handleFileIoName)
            source.Kernel.add_FileIOFileRundown(handle handleFileIoName)
        else
            source.Kernel.add_FileIOCreate(handleEventNoId handleFileIoCreate)
            source.Kernel.add_FileIOCleanup(handleEventNoId handleFileIoSimpleOp)
            source.Kernel.add_FileIOClose(handleEventNoId handleFileIoSimpleOp)
            source.Kernel.add_FileIOFlush(handleEventNoId handleFileIoSimpleOp)
            source.Kernel.add_FileIODelete(handleEventNoId handleFileIoInfo)
            source.Kernel.add_FileIOFSControl(handleEventNoId handleFileIoInfo)
            source.Kernel.add_FileIOQueryInfo(handleEventNoId handleFileIoInfo)
            source.Kernel.add_FileIOSetInfo(handleEventNoId handleFileIoInfo)
            source.Kernel.add_FileIORename(handleEventNoId handleFileIoInfo)
            source.Kernel.add_FileIORead(handleEventNoId handleFileIoReadWrite)
            source.Kernel.add_FileIOWrite(handleEventNoId handleFileIoReadWrite)
            source.Kernel.add_FileIOOperationEnd(handleEvent handleFileIoOpEnd)
            source.Kernel.add_FileIODirEnum(handleEventNoId handleFileIoDirEnum)


let createEtwHandler () =
    {
        KernelFlags = NtKeywords.FileIOInit ||| NtKeywords.FileIO
        KernelStackFlags = NtKeywords.FileIO
        KernelRundownFlags = NtKeywords.DiskFileIO ||| NtKeywords.DiskIO
        Providers = Array.empty<EtwEventProvider>
        Initialize = 
            fun (broadcast) -> ({
                Broadcast = broadcast
                PendingFileIo = DataCache<uint64, TraceEvent * array<struct (string * string)>>(256)
                FileIdToName = Dictionary<uint64, string>()
            } :> obj)
        Subscribe = subscribe
    }
