
module LowLevelDesign.WTrace.Tests.MetadataTests

open System
open System.IO
open NUnit.Framework
open FsUnit
open LowLevelDesign.WTrace.Tracing
open LowLevelDesign.WTrace

let path = Path.Combine(Path.GetTempPath(), "wtrace.db")

[<OneTimeTearDown>]
let TearDown () =
    if File.Exists(path) then
        File.Delete(path)

[<Test>]
let TestSessionConfig () =
    let metadata = EventMetadata.createMutable ()

    let startTime = DateTime(2000, 1, 1, 0, 0, 0)
    let sysEvent = SessionConfig (startTime, 100L, 10L)

    metadata.HandleMetadataEvent sysEvent

    // 10 qpcs equals 1 second (1000 ms)
    metadata.QpcToRelativeTimeInMs (Qpc 110L) |> should equal 1000.0

[<Test>]
let TestEventFieldNames () =

    let metadata = EventMetadata.createMutable ()
    let fieldNames = [| (0, 1, "fld1"); (0, 2, "fld2") |]
    fieldNames |> Array.iter ((EventFieldMetadata) >> metadata.HandleMetadataEvent)

    metadata.GetFieldName (0, 1) |> should equal "fld1"
    metadata.GetFieldName (0, 2) |> should equal "fld2"
    (fun () -> metadata.GetFieldName (0, 100) |> ignore) |> should throw typeof<InvalidOperationException>

[<Test>]
let TestEventProviders () =

    let metadata = EventMetadata.createMutable ()

    let providerId = Guid.NewGuid()
    metadata.HandleMetadataEvent (EventProvider (providerId, "test-provider"))

    [|
        (providerId, 1, "task1")
        (providerId, 2, "task2")
    |] |> Array.iter ((EventTask) >> metadata.HandleMetadataEvent)

    [|
        (providerId, 1, 1, "opcode11")
        (providerId, 1, 2, "opcode12")
        (providerId, 2, 2, "opcode22")
    |] |> Array.iter ((EventOpcode) >> metadata.HandleMetadataEvent)

    
    let providerId2 = Guid.NewGuid()
    metadata.HandleMetadataEvent (EventProvider (providerId2, "test-provider2"))
    [|
        (providerId2, 1, "task2-1")
    |] |> Array.iter ((EventTask) >> metadata.HandleMetadataEvent)
    [|
        (providerId2, 1, 2, "opcode2-12")
    |] |> Array.iter ((EventOpcode) >> metadata.HandleMetadataEvent)

    metadata.GetEventName (providerId, 1, 1) |> should equal "task1/opcode11"
    metadata.GetEventName (providerId, 1, 2) |> should equal "task1/opcode12"
    metadata.GetEventName (providerId, 2, 2) |> should equal "task2/opcode22"
    metadata.GetEventName (providerId, 3, 2) |> should equal "(3)/(2)"
    metadata.GetEventName (providerId, 3, 4) |> should equal "(3)/(4)"
    metadata.GetEventName (Guid.Empty, 3, 4) |> should equal "(3)/(4)"
    metadata.GetEventName (providerId2, 1, 2) |> should equal "task2-1/opcode2-12"
    
    // resend - should change only one opcode
    metadata.HandleMetadataEvent (EventProvider (providerId, "test-provider"))
    [|
        (providerId, 2, 2, "opcode22'")
    |] |> Array.iter ((EventOpcode) >> metadata.HandleMetadataEvent)
    
    metadata.GetEventName (providerId, 1, 1) |> should equal "task1/opcode11"
    metadata.GetEventName (providerId, 1, 2) |> should equal "task1/opcode12"
    metadata.GetEventName (providerId, 2, 2) |> should equal "task2/opcode22'"
    metadata.GetEventName (providerId, 3, 2) |> should equal "(3)/(2)"
    metadata.GetEventName (providerId, 3, 4) |> should equal "(3)/(4)"
    metadata.GetEventName (Guid.Empty, 3, 4) |> should equal "(3)/(4)"
    metadata.GetEventName (providerId2, 1, 2) |> should equal "task2-1/opcode2-12"

