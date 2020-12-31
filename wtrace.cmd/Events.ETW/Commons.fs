namespace LowLevelDesign.WTrace.Events.ETW

open System
open Microsoft.Diagnostics.Tracing
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events

type EtwEvent = Microsoft.Diagnostics.Tracing.TraceEvent

type NtKeywords = Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords

type EventPredicate = EtwEvent -> bool

type EtwEventProvider = {
    Id : Guid
    Name : string
    Level : TraceEventLevel
    Keywords : uint64
    RundownLevel : TraceEventLevel
    RundownKeywords : uint64
}

type EtwEventHandler = {
    KernelRundownFlags : NtKeywords
    KernelFlags : NtKeywords
    KernelStackFlags : NtKeywords
    Providers : array<EtwEventProvider>

    // unique id of the handler in the session * observer for the handler messages
    Initialize : EventBroadcast (* broadcast API *) -> obj (* handler state *)
    Subscribe : TraceEventSource (* ETW trace event source *) *
                bool (* isRundown *) *
                IdGenerator (* generates unique ids for events *) *
                obj(* handler state *) -> unit
}

module DotNetCommons =

    let clrProviderId = Guid(int32 0xe13c0d23, int16 0xccbc, int16 0x4e12, byte 0x93, byte 0x1b, byte 0xd9, byte 0xcc, byte 0x2e, byte 0xee, byte 0x27, byte 0xe4)

#nowarn "44" // disable the deprecation warning as we want to use TimeStampQPC

[<AutoOpen>]
module internal Commons =

    let handleEvent<'T, 'S when 'T :> EtwEvent> (idgen : IdGenerator) (state : 'S) handler (ev : 'T) : unit =
        handler (idgen()) state ev

    let handleEventNoId<'T, 'S when 'T :> EtwEvent> (state : 'S) handler (ev : 'T) : unit =
        handler ev.TimeStamp state ev

    let toEventField eventId struct (fieldName, fieldValue) =
        {
            EventId = eventId
            FieldName =  fieldName
            FieldValue = fieldValue
        }

    let toEvent (ev : EtwEvent) eventId activityId path details result =
        {
            EventId = eventId
            TimeStamp = ev.TimeStamp
            ActivityId = activityId
            Duration = TimeSpan.Zero
            ProcessId = ev.ProcessID
            ThreadId = ev.ThreadID
            EventName = ev.EventName
            EventLevel = int32 ev.Level
            Path = path
            Details = details
            Result = result
        }

