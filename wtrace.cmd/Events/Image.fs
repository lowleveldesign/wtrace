module LowLevelDesign.WTrace.Events.Image

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.HandlerCommons

type private ImageHandlerState = {
    Broadcast : EventBroadcast
}

[<AutoOpen>]
module private H =

    let handleImageLoad id state (ev : ImageLoadTraceData) =
        let details = sprintf "base: 0x%x" ev.ImageBase
        let path = sprintf "%s" ev.FileName
        let ev = {
            EventId = id
            TimeStamp = ev.TimeStamp
            ActivityId = ""
            Duration = TimeSpan.Zero
            ProcessId = ev.ProcessID
            ProcessName = ev.ProcessName
            ThreadId = ev.ThreadID
            EventName = if int32 ev.Opcode = 3 (* DCStart *) then "Image/Loaded" else ev.EventName
            EventLevel = int32 ev.Level
            Path = path
            Details = details
            Result = WinApi.eventStatusUndefined
        }
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, noFields))

    let subscribe (source : TraceEventSource, isRundown, idgen, state : obj) =
        let state = state :?> ImageHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        if isRundown then
            source.Kernel.add_ImageDCStart(handleEvent handleImageLoad)
        else
            source.Kernel.add_ImageLoad(handleEvent handleImageLoad)
            source.Kernel.add_ImageUnload(handleEvent handleImageLoad)


let createEtwHandler () =
    {
        KernelFlags = NtKeywords.ImageLoad
        KernelStackFlags = NtKeywords.None
        KernelRundownFlags = NtKeywords.ImageLoad
        Providers = Array.empty<EtwEventProvider>
        Initialize = 
            fun (broadcast) -> ({
                Broadcast = broadcast
            } :> obj)
        Subscribe = subscribe
    }

