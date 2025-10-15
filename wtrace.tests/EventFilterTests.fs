module LowLevelDesign.WTrace.Tests.EventFilterTests

open System
open NUnit.Framework
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
    Assert.That(ev |> filterFunction, Is.False)

    Assert.That({ ev with ProcessId = 1230 } |> filterFunction, Is.False)

    Assert.That({ ev with ProcessId = 1231 } |> filterFunction, Is.False)

    Assert.That({ ev with ProcessId = 1235 } |> filterFunction, Is.True)
    
    printfn "Elapsed time: %dms" sw.ElapsedMilliseconds

    let filterFunction =
        [| EventName ("~", "TestOpcode") |]
        |> EventFilter.buildFilterFunction
    Assert.That(ev |> filterFunction, Is.True)

    let filterFunction =
        [| EventName ("=", "TestTask/TestOpcode") |]
        |> EventFilter.buildFilterFunction
    Assert.That(ev |> filterFunction, Is.True)

    let filterFunction =
        [| EventName ("=", "TestTask") |]
        |> EventFilter.buildFilterFunction
    Assert.That(ev |> filterFunction, Is.False)

    let filterFunction = [| |] |> EventFilter.buildFilterFunction
    Assert.That(ev |> filterFunction, Is.True)

[<Test>]
let TestFilterParsing () =
    match EventFilter.parseFilter "name= Test" with
    | EventName (op, v) ->
        Assert.That(op, Is.EqualTo("="))
        Assert.That(v, Is.EqualTo("Test"))
    | _ -> Assert.Fail()

    Assert.That((fun () -> EventFilter.parseFilter "pid >= str" |> ignore), Throws.InstanceOf<EventFilter.ParseError>())

    match EventFilter.parseFilter "pid >= 10" with
    | ProcessId (op, v) ->
        Assert.That(op, Is.EqualTo(">="))
        Assert.That(v, Is.EqualTo(10))
    | _ -> Assert.Fail()

    match EventFilter.parseFilter "DETAILs    ~  test message   " with
    | Details (op, v) ->
        Assert.That(op, Is.EqualTo("~"))
        Assert.That(v, Is.EqualTo("test message"))
    | _ -> Assert.Fail()

    match EventFilter.parseFilter "  test event   " with
    | EventName (op, v) ->
        Assert.That(op, Is.EqualTo("~"))
        Assert.That(v, Is.EqualTo("test event"))
    | _ -> Assert.Fail()

    match EventFilter.parseFilter "level=debug" with
    | EventLevel (op, v) ->
        Assert.That(op, Is.EqualTo("="))
        Assert.That(v, Is.EqualTo(5))
    | _ -> Assert.Fail()

    match EventFilter.parseFilter "level ~ 1" with
    | EventLevel (op, v) ->
        Assert.That(op, Is.EqualTo("~"))
        Assert.That(v, Is.EqualTo(1))
    | _ -> Assert.Fail()
