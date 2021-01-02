module LowLevelDesign.WTrace.Events.ETW.IsrDpc

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.WinApi
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.FieldValues

type private IsrDpcHandlerState = {
    Broadcast : EventBroadcast
}

[<AutoOpen>]
module private H =

    let handleImageLoad id state (ev : ImageLoadTraceData) =
        if ev.ProcessID = 0 then // system process
            d

    let handleDpc id state (ev : DPCTraceData) =
        ()

    let handleIsr id state (ev : ISRTraceData) =
        ()
 
    let subscribe (source : TraceEventSource, isRundown, idgen, state : obj) =
        let state = state :?> IsrDpcHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        if isRundown then
            source.Kernel.add_ImageDCStart(handleEvent handleImageLoad)
        else
            source.Kernel.add_ImageLoad(handleEvent handleImageLoad)
            source.Kernel.add_PerfInfoDPC(handleEvent handleDpc)
            source.Kernel.add_PerfInfoThreadedDPC(handleEvent handleDpc)
            source.Kernel.add_PerfInfoTimerDPC(handleEvent handleDpc)
            source.Kernel.add_PerfInfoISR(handleEvent handleIsr)


let createEtwHandler () =
    {
        KernelFlags = NtKeywords.DeferedProcedureCalls ||| NtKeywords.Interrupt ||| NtKeywords.ImageLoad
        KernelStackFlags = NtKeywords.None
        KernelRundownFlags = NtKeywords.None
        Providers = Array.empty<EtwEventProvider>
        Initialize = fun (broadcast) -> { Broadcast = broadcast } :> obj
        Subscribe = subscribe
    }


