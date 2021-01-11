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

    let filterFunction = [| |] |> EventFilter.buildFilterFunction
    ev |> filterFunction |> should be True

[<Test>]
let TestFilterParsing () =
    match EventFilter.parseFilter "name= Test" with
    | EventName (op, v) ->
        op |> should equal "="
        v |> should equal "Test"
    | _ -> Assert.Fail()

    (fun () -> EventFilter.parseFilter "pid >= str" |> ignore) |> should throw typeof<ArgumentException>

    match EventFilter.parseFilter "pid >= 10" with
    | ProcessId (op, v) ->
        op |> should equal ">="
        v |> should equal 10
    | _ -> Assert.Fail()

    match EventFilter.parseFilter "DETAILs    ~  test message   " with
    | Details (op, v) ->
        op |> should equal "~"
        v |> should equal "test message"
    | _ -> Assert.Fail()

    match EventFilter.parseFilter "  test event   " with
    | EventName (op, v) ->
        op |> should equal "~"
        v |> should equal "test event"
    | _ -> Assert.Fail()

    match EventFilter.parseFilter "level=debug" with
    | EventLevel (op, v) ->
        op |> should equal "="
        v |> should equal 5
    | _ -> Assert.Fail()

    match EventFilter.parseFilter "level ~ 1" with
    | EventLevel (op, v) ->
        op |> should equal "~"
        v |> should equal 1
    | _ -> Assert.Fail()
