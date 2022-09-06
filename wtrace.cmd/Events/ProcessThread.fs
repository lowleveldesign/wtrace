module LowLevelDesign.WTrace.Events.ProcessThread

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.HandlerCommons

type private ProcessThreadHandlerState = {
    Broadcast : EventBroadcast
}

[<AutoOpen>]
module private H =

    let handleProcessEvent id state (ev : ProcessTraceData) =
        let status = 
            let opcode = int32 ev.Opcode
            if opcode = 1 (* Start *) || opcode = 3 (* DCStart *) then WinApi.eventStatusUndefined
            else ev.ExitStatus

        let fields =
            [|
                struct (nameof ev.ParentID, FI32 ev.ParentID)
                struct (nameof ev.CommandLine, FText ev.CommandLine)
                struct (nameof ev.SessionID, FI32 ev.SessionID)
                struct (nameof ev.ImageFileName, FText ev.ImageFileName)
                struct (nameof ev.PackageFullName, FText ev.PackageFullName)
                struct (nameof ev.ApplicationID, FText ev.ApplicationID)
            |] |> Array.map (toEventField id)

        let details = sprintf "command line: '%s'" ev.CommandLine
        let ev = toEvent ev id "" "" details status
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields))

    let handleThreadEvent id state (ev : ThreadTraceData) =
        let threadName = ev.ThreadName
        let traceEvent =
            if (String.IsNullOrEmpty(threadName)) then
                let fields = [|
                    struct (nameof ev.ThreadFlags, FI32 ev.ThreadFlags)
                    |> toEventField id
                |]
                TraceEventWithFields (toEvent ev id "" "" "" WinApi.eventStatusUndefined, fields)
            else
                let fields = [|
                    struct (nameof ev.ThreadName, FText threadName)
                    struct (nameof ev.ThreadFlags, FI32 ev.ThreadFlags)
                |]

                let details = sprintf "name: %s" threadName
                TraceEventWithFields (toEvent ev id "" "" details WinApi.eventStatusUndefined, fields |> Array.map (toEventField id))

        state.Broadcast.publishTraceEvent traceEvent
 
    let subscribe (source : TraceEventSources, isRundown, idgen, state : obj) =
        let state = state :?> ProcessThreadHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        if isRundown then
            source.Kernel.add_ProcessDCStart(handleEvent handleProcessEvent)
        else
            source.Kernel.add_ProcessStart(handleEvent handleProcessEvent)
            source.Kernel.add_ProcessStop(handleEvent handleProcessEvent)
            source.Kernel.add_ThreadStart(handleEvent handleThreadEvent)
            source.Kernel.add_ThreadStop(handleEvent handleThreadEvent)


let createEtwHandler () =
    {
        KernelFlags = NtKeywords.Process ||| NtKeywords.Thread
        KernelStackFlags = NtKeywords.Process ||| NtKeywords.Thread
        KernelRundownFlags = NtKeywords.Process
        Providers = Array.empty<EtwEventProvider>
        Initialize = fun broadcast -> { Broadcast = broadcast } :> obj
        Subscribe = subscribe
    }

