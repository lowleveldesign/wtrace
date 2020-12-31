namespace LowLevelDesign.WTrace

open System

(**** Classes describing trace events ****)

type TraceEventField = {
    EventId : int32
    FieldId : int32
    FieldValue : string
}

type TraceEvent = {
    EventId : int32
    TimeStamp : Qpc
    Duration : Qpc
    ProcessId : int32
    ThreadId : int32
    HandlerId : int32
    ProviderId : Guid
    TaskId : int32
    OpcodeId : int32
    EventLevel : int32
    Path : string
    Details : string
    Result : int32
}

type TraceEventWithFields = TraceEventWithFields of TraceEvent * array<TraceEventField>

(**** Metadata events ****)

type MetadataEvent =
| EventFieldMetadata of HandlerId : int32 * FieldId : int32 * FieldName : string
| EventProvider of Id : Guid * Name : string
| EventTask of ProviderId : Guid * Id : int32 * Name : string
| EventOpcode of ProviderId : Guid * TaskId : int32 * Id : int32 * Name : string
| SessionConfig of StartTimeUtc : DateTime * StartTimeQpc : int64 * QpcFreq : int64

