module LowLevelDesign.WTrace.Events.Alpc

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.HandlerCommons

type private AlpcHandlerState = {
    Broadcast : EventBroadcast
    // a state to keep information about the pending RPC calls
    PendingAlpcCalls : DataCache<int32, string * DateTime * int32 * string * int32>
}

[<AutoOpen>]
module private H =

    let handleAlpcSendMessage state (ev : ALPCSendMessageTraceData) =
        state.PendingAlpcCalls.[ev.MessageID] <- (ev.EventName, ev.TimeStamp, ev.ProcessID, ev.ProcessName, ev.ThreadID)

    let handleAlpcReceiveMessage id state (ev : ALPCReceiveMessageTraceData) =
        let msgid = ev.MessageID
        match state.PendingAlpcCalls.TryGetValue(msgid) with
        | (true, (eventName, ts, pid, pname, tid)) ->
            state.PendingAlpcCalls.Remove(msgid) |> ignore
            let tev =
                {
                    EventId = id; TimeStamp = ev.TimeStamp; ActivityId = $"ALPC#0x{ev.MessageID:X}"
                    Duration = ev.TimeStamp - ts; ProcessId = pid
                    ProcessName = pname; ThreadId = tid; EventName = eventName
                    EventLevel = int32 ev.Level; Path = ""; Result = WinApi.eventStatusUndefined
                    Details = $"{pname} ---(0x{ev.MessageID:X})--> {ev.ProcessName} ({ev.ProcessID}.{ev.ThreadID})"
                }
            TraceEventWithFields (tev, [| |]) |> state.Broadcast.publishTraceEvent
            let tev =
                {
                    tev with
                        EventName = ev.EventName
                        ProcessId = ev.ProcessID
                        ProcessName = ev.ProcessName
                        ThreadId = ev.ThreadID
                        Details = $"{ev.ProcessName} <--(0x{ev.MessageID:X})--- {pname} ({pid}.{tid})"
                }
            TraceEventWithFields (tev, [| |]) |> state.Broadcast.publishTraceEvent
        | (false, _) ->
            let ev =
                {
                    EventId = id; TimeStamp = ev.TimeStamp; ActivityId = $"ALPC#0x{ev.MessageID:X}"
                    Duration = TimeSpan.Zero; ProcessId = ev.ProcessID
                    ProcessName = ev.ProcessName; ThreadId = ev.ThreadID; EventName = ev.EventName
                    EventLevel = int32 ev.Level; Path = ""; Result = WinApi.eventStatusUndefined
                    Details = $"{ev.ProcessName} <--(0x{ev.MessageID:X})--- ??"
                }
            TraceEventWithFields (ev, [| |]) |> state.Broadcast.publishTraceEvent

    let subscribe (source : TraceEventSource, isRundown, idgen, state : obj) =
        let state = state :?> AlpcHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        let handleEventNoId h = Action<_>(handleEventNoId state h)
        if not isRundown then
            source.Kernel.add_ALPCSendMessage(handleEventNoId handleAlpcSendMessage)
            source.Kernel.add_ALPCReceiveMessage(handleEvent handleAlpcReceiveMessage)

let createEtwHandler () =
    {
        KernelFlags = NtKeywords.AdvancedLocalProcedureCalls
        KernelStackFlags = NtKeywords.AdvancedLocalProcedureCalls
        KernelRundownFlags = NtKeywords.None
        Providers = [| |]
        Initialize = 
            fun (broadcast) -> ({
                Broadcast = broadcast
                PendingAlpcCalls = DataCache<int32, string * DateTime * int32 * string * int32>(2000)
            } :> obj)
        Subscribe = subscribe
    }
