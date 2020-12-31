namespace LowLevelDesign.WTrace.Tracing

open System
open LowLevelDesign.WTrace

type RealtimeTraceSessionSettings = {
    EnableStacks: bool
    TraceFilter : TraceEventWithFields -> bool
} 

type RealtimeSessionStatus = SessionRunning of EventsLost: int32

[<AutoOpen>]
module Commons =

    let getProcessName (imageFileName: string) =
        Debug.Assert(not (String.IsNullOrEmpty(imageFileName)), "[tracedata] imageFileName is empty")

        let separator =
            imageFileName.LastIndexOfAny([| '/'; '\\' |])

        let extension = imageFileName.LastIndexOf('.')
        match struct (separator, extension) with
        | struct (-1, -1) -> imageFileName
        | struct (-1, x) -> imageFileName.Substring(0, x)
        | struct (s, -1) -> imageFileName.Substring(s + 1)
        | struct (s, x) when x > s + 1 -> imageFileName.Substring(s + 1, x - s - 1)
        | _ -> imageFileName
