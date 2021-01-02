module LowLevelDesign.WTrace.Events.ETW.ProcessThread

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.WinApi
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.FieldValues

type private ProcessThreadHandlerState = {
    Broadcast : EventBroadcast
}

[<AutoOpen>]
module private H =

    let handleProcessEvent id state (ev : ProcessTraceData) =
        let status = 
            let opcode = int32 ev.Opcode
            if opcode = 1 (* Start *) || opcode = 3 (* DCStart *) then eventStatusUndefined
            else ev.ExitStatus

        let fields =
            [|
                struct (nameof ev.ParentID, ev.ParentID |> i32s)
                struct (nameof ev.CommandLine, ev.CommandLine |> s2s)
                struct (nameof ev.SessionID, ev.SessionID |> i32s)
                struct (nameof ev.ImageFileName, ev.ImageFileName |> s2s)
                struct (nameof ev.PackageFullName, ev.PackageFullName |> s2s)
                struct (nameof ev.ApplicationID, ev.ApplicationID |> s2s)
            |] |> Array.map (toEventField id)

        let details = sprintf "command line: '%s'" ev.CommandLine
        let ev = toEvent ev id "" "" details status
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields))

    let handleThreadEvent id state (ev : ThreadTraceData) =
        let threadName = ev.ThreadName
        let traceEvent =
            if (String.IsNullOrEmpty(threadName)) then
                let fields = [|
                    struct (nameof ev.ThreadFlags, ev.ThreadFlags |> i32s)
                    |> toEventField id
                |]
                TraceEventWithFields (toEvent ev id "" "" "" eventStatusUndefined, fields)
            else
                let fields = [|
                    struct (nameof ev.ThreadName, threadName |> s2s)
                    struct (nameof ev.ThreadFlags, ev.ThreadFlags |> i32s)
                |]

                let details = sprintf "name: %s" threadName
                TraceEventWithFields (toEvent ev id "" "" details eventStatusUndefined, fields |> Array.map (toEventField id))

        state.Broadcast.publishTraceEvent traceEvent
 
    let subscribe (source : TraceEventSource, isRundown, idgen, state : obj) =
        let state = state :?> ProcessThreadHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        if isRundown then
            let h (ev : ProcessTraceData) =
                handleProcessEvent (idgen()) state ev

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
        Initialize = fun (broadcast) -> { Broadcast = broadcast } :> obj
        Subscribe = subscribe
    }


