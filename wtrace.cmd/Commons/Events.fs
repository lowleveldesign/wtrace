namespace LowLevelDesign.WTrace

open System
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers

(**** Classes describing trace events ****)

type TraceEventFieldValue =
| FText of string
| FI32 of int32
| FUI32 of uint32
| FI64 of int64
| FUI64 of uint64
| FF64 of float
| FGuid of Guid

type TraceEventField = {
    EventId : int32
    FieldName : string
    FieldValue : TraceEventFieldValue
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

type TraceEventSources (source : TraceEventSource) =

    let rpc = MicrosoftWindowsRPCTraceEventParser(source)

    member this.Kernel = source.Kernel

    member this.Rpc = rpc

