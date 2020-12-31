
module LowLevelDesign.WTrace.Tests.SystemEventTests

open System.IO
open NUnit.Framework
open FsUnit
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events.ETW
open LowLevelDesign.WTrace.Tracing
open LowLevelDesign.WTrace.Events.FieldValues
open LowLevelDesign.WTrace.WinApi

let path = Path.Combine(Path.GetTempPath(), "wtrace.db")

[<OneTimeTearDown>]
let TearDown () =
    if File.Exists(path) then
        File.Delete(path)

[<Test>]
let TestProcessEvents () =
    let traceinfo = TraceData.createMutable (NoFilter)
    
    let ev = {
        EventId = 1; TimeStamp = Qpc 1L; Duration = Qpc 1L; ProcessId = 1
        ThreadId = 1; HandlerId = 1; ProviderId = kernelProviderId; TaskId = 1;
        OpcodeId = 3 (* Rundown *); EventLevel = 0; Path = "non-existing-path"; Details = "short details"
        Result = 0
    }
    let fields = [|
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.ParentID; FieldValue = 4 |> i32db }
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.ImageFileName; FieldValue = @"C:\Windows\cmd.exe" |> s2db }
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.CommandLine; FieldValue = @"cmd" |> s2db }
    |]
    TraceEventWithFields (ev, fields)
    |> traceinfo.HandleAndFilterSystemEvent
    |> ignore
    TraceEventWithFields ({ ev with OpcodeId = 2; TimeStamp = Qpc 99L; Result = 0 }, Array.empty<TraceEventField>)
    |> traceinfo.HandleAndFilterSystemEvent
    |> ignore

    let ev = {ev with TimeStamp = Qpc 100L; OpcodeId = 1 (* Process start *) }
    let fields = [|
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.ParentID; FieldValue = 2 |> i32db }
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.ImageFileName; FieldValue = @"C:\Windows\notepad.exe" |> s2db }
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.CommandLine; FieldValue = @"notepad" |> s2db }
    |]
    TraceEventWithFields (ev, fields)
    |> traceinfo.HandleAndFilterSystemEvent
    |> ignore
    TraceEventWithFields ({ ev with OpcodeId = 2; TimeStamp = Qpc 110L; Result = 0 }, Array.empty<TraceEventField>)
    |> traceinfo.HandleAndFilterSystemEvent
    |> ignore

    let ev = {ev with TimeStamp = Qpc 111L }
    let fields = [|
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.ParentID; FieldValue = 3 |> i32db }
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.ImageFileName; FieldValue = @"C:\Windows\mspaint.exe" |> s2db }
        { EventId = 1; FieldId = int32 ProcessThread.FieldId.CommandLine; FieldValue = @"mspaint" |> s2db }
    |]
    TraceEventWithFields (ev, fields)
    |> traceinfo.HandleAndFilterSystemEvent
    |> ignore
    
    let p = traceinfo.FindProcess struct (1, (Qpc 70L))

    p.ProcessName |> should equal "cmd"
    p.StartTime |> should equal QpcMin
    p.ExitTime |> should equal (Qpc 99L)

    let p = traceinfo.FindProcess struct (1, (Qpc 120L))

    p.ProcessName |> should equal "mspaint"
    p.StartTime |> should equal (Qpc 111L)
    p.ExitTime |> should equal QpcMax

    let p = traceinfo.FindProcess struct (1, (Qpc 101L))

    p.ProcessName |> should equal "notepad"
    p.StartTime |> should equal (Qpc 100L)
    p.ExitTime |> should equal (Qpc 110L)

    let unknownProcess = {
        Pid = 2
        ParentPid = -1
        ProcessName = "??"
        ImageFileName = "??"
        CommandLine = "??"
        StartTime = QpcMin
        ExitTime = QpcMax
        ExitStatus = -1
        ExtraInfo = ""
    }
    traceinfo.FindProcess struct (2, (Qpc 0L)) |> should equal unknownProcess

