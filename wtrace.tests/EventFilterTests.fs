module LowLevelDesign.WTrace.Tests.EventFilterTests

open System
open NUnit.Framework
open FsUnit
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Tracing


[<Test>]
let TestRealtimeFilters () =
    let now = DateTime.Now

    let ev = {
        TraceEvent.EventId = 1
        TimeStamp = now
        Duration = TimeSpan.Zero
        ActivityId = ""
        ProcessId = 1
        ProcessName = "test"
        ThreadId = 1
        EventName = "TestTask/TestOpcode"
        EventLevel = 1
        Path = "non-existing-path"
        Details = "short details"
        Result = 1
    }
    
    let filterFunction =
        [|
            ProcessName ("~", "test")
            ProcessId (">=", 1234)
        |] |> EventFilter.buildFilterFunction

    let sw = System.Diagnostics.Stopwatch.StartNew()
    ev |> filterFunction |> should be False

    { ev with ProcessId = 1230 } |> filterFunction |> should be False

    { ev with ProcessId = 1231 } |> filterFunction |> should be False

    { ev with ProcessId = 1235 } |> filterFunction |> should be True
    
    printfn "Elapsed time: %dms" sw.ElapsedMilliseconds

    let filterFunction =
        [| EventName ("~", "TestOpcode") |]
        |> EventFilter.buildFilterFunction
    ev |> filterFunction |> should be True

    let filterFunction =
        [| EventName ("=", "TestTask/TestOpcode") |]
        |> EventFilter.buildFilterFunction
    ev |> filterFunction |> should be True

    let filterFunction =
        [| EventName ("=", "TestTask") |]
        |> EventFilter.buildFilterFunction
    ev |> filterFunction |> should be False

