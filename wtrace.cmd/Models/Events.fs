namespace LowLevelDesign.WTrace

open System

(**** Classes describing trace events ****)

type TraceEventField = {
    EventId : int32
    FieldName : string
    FieldValue : string
}

type TraceEvent = {
    EventId : int32
    TimeStamp : DateTime
    Duration : TimeSpan
    ProcessId : int32
    ProcessName : string
    ThreadId : int32
    ActivityId : string
    EventName : string
    EventLevel : int32
    Path : string
    Details : string
    Result : int32
}

type TraceEventWithFields = TraceEventWithFields of TraceEvent * array<TraceEventField>

