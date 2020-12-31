module LowLevelDesign.WTrace.Tests.EventFilterTests

open System
open NUnit.Framework
open FsUnit
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Tracing
open LowLevelDesign.WTrace.Events.ETW
open LowLevelDesign.WTrace.WinApi
open LowLevelDesign.WTrace.Events.FieldValues


[<Test>]
let TestRealtimeFilters () =
    let metadata = EventMetadata.createMutable()
    let tracedata = TraceData.createMutable(NoFilter)

    let ev = {
        EventId = 1; TimeStamp = Qpc 1L; Duration = Qpc 1L; ProcessId = 1230
        ThreadId = 1; HandlerId = 1; ProviderId = kernelProviderId; TaskId = 1;
        OpcodeId = 1 (* Process start *); EventLevel = 0; Path = "non-existing-path"; Details = "short details"
        Result = 0
    }
    let fields = [|
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.ParentID; FieldValue = 0 |> i32db }
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.ImageFileName; FieldValue = @"test" |> s2db }
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.CommandLine; FieldValue = @"" |> s2db }
    |]
    TraceEventWithFields (ev, fields)
    |> tracedata.HandleAndFilterSystemEvent
    |> ignore

    TraceEventWithFields ({ ev with ProcessId = 1235 }, fields)
    |> tracedata.HandleAndFilterSystemEvent
    |> ignore

    fields.[1] <- { fields.[1] with FieldValue = "untestable" |> s2db }
    TraceEventWithFields ({ ev with ProcessId = 1231 }, fields)
    |> tracedata.HandleAndFilterSystemEvent
    |> ignore



    let providerId = Guid.NewGuid()
    [|
        EventProvider (providerId, "Test")
        MetadataEvent.EventTask (providerId, 1, "TestTask")
        MetadataEvent.EventOpcode (providerId, 1, 1, "TestOpcode")
    |] |> Seq.iter metadata.HandleMetadataEvent

    let ev = {
        TraceEvent.EventId = 1
        TimeStamp = Qpc 1L
        Duration = Qpc 1L
        ProcessId = 1
        ThreadId = 1
        ProviderId = providerId
        HandlerId = 1
        TaskId = 1
        OpcodeId = 1
        EventLevel = 1
        Path = "non-existing-path"
        Details = "short details"
        Result = 1
    }
    
    let filterFunction =
        [|
            ProcessName ("Contains", "test")
            ProcessId ("GreaterThanOrEqualTo", 1234)
        |] |> EventFilter.buildFilterFunction metadata tracedata

    let sw = System.Diagnostics.Stopwatch.StartNew()
    ev |> filterFunction |> should be False

    tracedata.FindProcess(struct (1230, Qpc 1L)).ProcessName |> should equal "test"
    { ev with ProcessId = 1230 } |> filterFunction |> should be False

    tracedata.FindProcess(struct (1231, Qpc 1L)).ProcessName |> should equal "untestable"
    { ev with ProcessId = 1231 } |> filterFunction |> should be False

    { ev with ProcessId = 1235 } |> filterFunction |> should be True
    
    printfn "Elapsed time: %dms" sw.ElapsedMilliseconds

    let filterFunction =
        [| EventName ("Contains", "TestOpcode") |]
        |> EventFilter.buildFilterFunction metadata tracedata
    ev |> filterFunction |> should be True

    let filterFunction =
        [| EventName ("EqualTo", "TestTask/TestOpcode") |]
        |> EventFilter.buildFilterFunction metadata tracedata
    ev |> filterFunction |> should be True

    let filterFunction =
        [| EventName ("EqualTo", "TestTask") |]
        |> EventFilter.buildFilterFunction metadata tracedata
    ev |> filterFunction |> should be False
