namespace LowLevelDesign.WTrace.Events.ETW

open System
open Microsoft.Diagnostics.Tracing
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events

type EtwEvent = Microsoft.Diagnostics.Tracing.TraceEvent

type NtKeywords = Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords

type EventPredicate = EtwEvent -> bool

type EventFieldDesc = (struct (int32 * string * string))

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
    Initialize : int32 (* handler id *) * EventBroadcast (* broadcast API *) -> obj (* handler state *)
    Subscribe : TraceEventSource (* ETW trace event source *) * bool (* isRundown *) *
                IdGenerator (* generates unique ids for events *) *
                TimeStampAdjust (* adjusts event timestamp to the session start time *) *
                obj(* handler state *) -> unit
}

module DotNetCommons =

    let clrProviderId = Guid(int32 0xe13c0d23, int16 0xccbc, int16 0x4e12, byte 0x93, byte 0x1b, byte 0xd9, byte 0xcc, byte 0x2e, byte 0xee, byte 0x27, byte 0xe4)

#nowarn "44" // disable the deprecation warning as we want to use TimeStampQPC

[<AutoOpen>]
module internal Commons =
    
    let publishHandlerMetadata metadata publish =
        metadata |> Array.iter (fun m -> (publish m))

    let handleEvent<'T, 'S when 'T :> EtwEvent> (idgen : IdGenerator) (tsadj : TimeStampAdjust) (state : 'S) handler (ev : 'T) : unit =
        handler (idgen()) (tsadj (ev.TimeStampQPC)) state ev

    let handleEventNoId<'T, 'S when 'T :> EtwEvent> (tsadj : TimeStampAdjust) (state : 'S) handler (ev : 'T) : unit =
        handler (tsadj (ev.TimeStampQPC)) state ev

    let toEventField eventId struct (fieldId, _, fieldValue) =
        {
            EventId = eventId
            FieldId =  fieldId
            FieldValue = fieldValue
        }

    let toEvent handlerId (ev : EtwEvent) eventId ts path details result =
        {
            EventId = eventId
            TimeStamp = Qpc ts
            Duration = Qpc 0L
            ProcessId = ev.ProcessID
            ThreadId = ev.ThreadID
            HandlerId = handlerId
            ProviderId = ev.ProviderGuid
            TaskId = int32 ev.Task
            OpcodeId = int32 ev.Opcode
            EventLevel = int32 ev.Level
            Path = path
            Details = details
            Result = result
        }

    let publishEventFieldsMetadata<'f when 'f : enum<int32>> handlerId publish =
        let names = Enum.GetNames(typedefof<'f>)
        let values = Enum.GetValues(typedefof<'f>) :?> array<int32>
        Debug.Assert(names.Length = values.Length, "[commons] names.Length = values.Length")
        names |> Seq.iteri (fun i n -> publish (EventFieldMetadata (handlerId, values.[i], n)))

