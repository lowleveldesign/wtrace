module LowLevelDesign.WTrace.Events.IsrDpc

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.HandlerCommons

type private IsrDpcHandlerState = {
    Broadcast : EventBroadcast
}

[<AutoOpen>]
module private H =

    let handleImageLoad id state (ev : ImageLoadTraceData) =
        if ev.ProcessID = 0 then // system process
            let fields =
                [|
                    struct (nameof ev.ImageBase, FUI64 ev.ImageBase)
                    struct (nameof ev.ImageSize, FI32 ev.ImageSize)
                |] |> Array.map (toEventField id)

            let eventName =
                if ev.OpcodeName = "DCStart" || ev.OpcodeName = "Load" then
                    "SystemImage/Load"
                else
                    Debug.Assert(ev.OpcodeName = "Unload", "Unexpected image operation")
                    "SystemImage/Unload"
            let ev = {
                EventId = id
                TimeStamp = ev.TimeStamp
                ActivityId = ""
                Duration = TimeSpan.Zero
                ProcessId = ev.ProcessID
                ProcessName = ev.ProcessName
                ThreadId = ev.ThreadID
                EventName = eventName
                EventLevel = int32 ev.Level
                Path = ev.FileName
                Details = ""
                Result = WinApi.eventStatusUndefined
            }

            state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields))

    let handleDpc id state (ev : DPCTraceData) =
        let fields =
            [|
                struct (nameof ev.ElapsedTimeMSec, FF64 ev.ElapsedTimeMSec)
                struct (nameof ev.Routine, FUI64 ev.Routine)
            |] |> Array.map (toEventField id)

        let ev = toEvent ev id "" "" "" WinApi.eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields))

    let handleIsr id state (ev : ISRTraceData) =
        let fields =
            [|
                struct (nameof ev.ElapsedTimeMSec, FF64 ev.ElapsedTimeMSec)
                struct (nameof ev.Routine, FUI64 ev.Routine)
            |] |> Array.map (toEventField id)

        let ev = toEvent ev id "" "" "" WinApi.eventStatusUndefined
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, fields))
 
    let subscribe (source : TraceEventSources, isRundown, idgen, state : obj) =
        let state = state :?> IsrDpcHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        if isRundown then
            source.Kernel.add_ImageDCStart(handleEvent handleImageLoad)
        else
            source.Kernel.add_ImageLoad(handleEvent handleImageLoad)
            source.Kernel.add_ImageUnload(handleEvent handleImageLoad)
            source.Kernel.add_PerfInfoDPC(handleEvent handleDpc)
            source.Kernel.add_PerfInfoThreadedDPC(handleEvent handleDpc)
            source.Kernel.add_PerfInfoTimerDPC(handleEvent handleDpc)
            source.Kernel.add_PerfInfoISR(handleEvent handleIsr)


let createEtwHandler () =
    {
        KernelFlags = NtKeywords.DeferedProcedureCalls ||| NtKeywords.Interrupt ||| NtKeywords.ImageLoad
        KernelStackFlags = NtKeywords.None
        KernelRundownFlags = NtKeywords.ImageLoad
        Providers = Array.empty<EtwEventProvider>
        Initialize = fun broadcast -> { Broadcast = broadcast } :> obj
        Subscribe = subscribe
    }

