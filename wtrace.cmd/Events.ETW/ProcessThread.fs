module LowLevelDesign.WTrace.Events.ETW.ProcessThread

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.WinApi
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.FieldValues

type private ProcessThreadHandlerState = {
    HandlerId : int32
    Broadcast : EventBroadcast
}

type FieldId =
| ParentID = 0 | CommandLine = 1 | SessionID = 2
| ImageFileName = 3 | PackageFullName = 4 | ApplicationID = 5
| ThreadName = 6 | ThreadFlags = 7

let metadata = [|
    EventProvider (kernelProviderId, "Kernel")
    EventTask (kernelProviderId, 1, "Process")
    EventTask (kernelProviderId, 2, "Thread")
    EventOpcode (kernelProviderId, 1, 1, "Start")
    EventOpcode (kernelProviderId, 1, 2, "Stop")
    EventOpcode (kernelProviderId, 1, 3, "DCStart")
    EventOpcode (kernelProviderId, 1, 4, "DCStop")
    EventOpcode (kernelProviderId, 2, 1, "Start")
    EventOpcode (kernelProviderId, 2, 2, "Stop")
    EventOpcode (kernelProviderId, 2, 3, "DCStart")
    EventOpcode (kernelProviderId, 2, 4, "DCStop")
|]

#nowarn "44" // disable the deprecation warning as we want to use TimeStampQPC

[<AutoOpen>]
module private H =

    let handleProcessEvent id ts state (ev : ProcessTraceData) =
        let status = 
            let opcode = int32 ev.Opcode
            if opcode = 1 (* Start *) || opcode = 3 (* DCStart *) then eventStatusUndefined
            else ev.ExitStatus
        
        let fields =
            [|
                struct (int32 FieldId.ParentID, nameof FieldId.ParentID, ev.ParentID |> i32db)
                struct (int32 FieldId.CommandLine, nameof FieldId.CommandLine, ev.CommandLine |> s2db)
                struct (int32 FieldId.SessionID, nameof FieldId.SessionID, ev.SessionID |> i32db)
                struct (int32 FieldId.ImageFileName, nameof FieldId.ImageFileName, ev.ImageFileName |> s2db)
                struct (int32 FieldId.PackageFullName, nameof FieldId.PackageFullName, ev.PackageFullName |> s2db)
                struct (int32 FieldId.ApplicationID, nameof FieldId.ApplicationID, ev.ApplicationID |> s2db)
            |] |> Array.map (toEventField id)

        let details = sprintf "command line: '%s'" ev.CommandLine
        let ev = toEvent state.HandlerId ev id ts "" details status
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields))

    let handleThreadEvent id ts state (ev : ThreadTraceData) =
        let threadName = ev.ThreadName
        let traceEvent =
            if (String.IsNullOrEmpty(threadName)) then
                let fields = [|
                    struct (int32 FieldId.ThreadFlags, nameof FieldId.ThreadFlags, ev.ThreadFlags |> i32db)
                    |> toEventField id
                |]
                TraceEventWithFields (toEvent state.HandlerId ev id ts "" "" eventStatusUndefined, fields)
            else
                let fields = [|
                    struct (int32 FieldId.ThreadName, nameof FieldId.ThreadName, threadName |> s2db)
                    struct (int32 FieldId.ThreadFlags, nameof FieldId.ThreadFlags, ev.ThreadFlags |> i32db)
                |]

                let details = sprintf "name: %s" threadName
                TraceEventWithFields (toEvent state.HandlerId ev id ts "" details eventStatusUndefined, fields |> Array.map (toEventField id))

        state.Broadcast.publishTraceEvent traceEvent
 
    let subscribe (source : TraceEventSource, isRundown, idgen, tsadj, state : obj) =
        let state = state :?> ProcessThreadHandlerState
        let handleEvent h = Action<_>(handleEvent idgen tsadj state h)
        if isRundown then
            publishHandlerMetadata metadata state.Broadcast.publishMetaEvent
            publishEventFieldsMetadata<FieldId> state.HandlerId state.Broadcast.publishMetaEvent
            
            let h (ev : ProcessTraceData) =
                handleProcessEvent (idgen()) (tsadj(ev.TimeStampQPC)) state ev

            source.Kernel.add_ProcessDCStart(Action<_>(h))
        else
            source.Kernel.add_ProcessStart(handleEvent handleProcessEvent)
            source.Kernel.add_ProcessStop(handleEvent handleProcessEvent)
            source.Kernel.add_ThreadStart(handleEvent handleThreadEvent)
            source.Kernel.add_ThreadStop(handleEvent handleThreadEvent)


let createEtwHandler () =
    {
        KernelFlags = NtKeywords.Process ||| NtKeywords.Thread
        KernelStackFlags = NtKeywords.Process ||| NtKeywords.Thread
        KernelRundownFlags = NtKeywords.None
        Providers = Array.empty<EtwEventProvider>
        Initialize = fun (id, broadcast) -> { HandlerId = id; Broadcast = broadcast } :> obj
        Subscribe = subscribe
    }


