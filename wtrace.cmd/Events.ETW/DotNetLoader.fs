module LowLevelDesign.WTrace.Events.ETW.DotNetLoader

open FSharp.Collections
open System
open System.Collections.Generic
open System.IO
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open Microsoft.Diagnostics.Tracing
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.ETW
open LowLevelDesign.WTrace.Events.ETW.DotNetCommons
open LowLevelDesign.WTrace.Events.FieldValues
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.WinApi
open Microsoft.Diagnostics.Tracing.Parsers.Clr

type private State = {
    HandlerId : int32
    Broadcast : EventBroadcast
}

let metadata = [|
    EventProvider (clrProviderId, ".NET")
    EventTask (clrProviderId, 10, ".NET")
    EventOpcode (clrProviderId, 10, 33, "ModuleLoad")
    EventOpcode (clrProviderId, 10, 34, "ModuleUnload")
    EventOpcode (clrProviderId, 10, 37, "AssemblyLoad")
    EventOpcode (clrProviderId, 10, 38, "AssemblyUnload")
|]

type FieldId =
| ModuleILPath = 0 | ModuleNativePath = 1 | ModuleFlags = 2
| AssemblyID = 3 | ModuleID = 4

#nowarn "44" // disable the deprecation warning as we want to use TimeStampQPC

[<AutoOpen>]
module private H =

    let handleModuleLoadUnload id ts state (ev : ModuleLoadUnloadTraceData) =
        let flags = sprintf "%A" ev.ModuleFlags
        let ilPath = ev.ModuleILPath
        let nativePath = ev.ModuleNativePath
        let fields =
            [|
                struct (int32 FieldId.ModuleILPath, nameof FieldId.ModuleILPath, ilPath |> s2db)
                struct (int32 FieldId.ModuleNativePath, nameof FieldId.ModuleNativePath, nativePath |> s2db)
                struct (int32 FieldId.ModuleFlags, nameof FieldId.ModuleFlags, flags |> s2db)
                struct (int32 FieldId.AssemblyID, nameof FieldId.AssemblyID, sprintf "0x%x" ev.AssemblyID |> s2db)
                struct (int32 FieldId.ModuleID, nameof FieldId.ModuleID, sprintf "0x%x" ev.ModuleID |> s2db)
            |] |> Array.map (toEventField id)

        let details = sprintf "flags: %s" flags
        let path = if not (String.IsNullOrEmpty(nativePath)) then nativePath else ilPath
        TraceEventWithFields (toEvent state.HandlerId ev id ts path details eventStatusUndefined, fields)
        |> state.Broadcast.publishTraceEvent

    let subscribe (source : TraceEventSource, isRundown, idgen, tsadj, state : obj) =
        let state = state :?> State
        let handleEvent h = Action<_>(handleEvent idgen tsadj state h)
        if isRundown then
            publishHandlerMetadata metadata state.Broadcast.publishMetaEvent
            publishEventFieldsMetadata<FieldId> state.HandlerId state.Broadcast.publishMetaEvent
        else
            source.Clr.add_LoaderModuleLoad(handleEvent handleModuleLoadUnload)
            source.Clr.add_LoaderModuleUnload(handleEvent handleModuleLoadUnload)
            // FIXME: assembly

let createEtwHandler () =
    {
        KernelFlags = NtKeywords.None
        KernelStackFlags = NtKeywords.None
        KernelRundownFlags = NtKeywords.None
        Providers = [|
            { Id = clrProviderId; Name = "Microsoft-Windows-DotNETRuntime";
              Level = TraceEventLevel.Verbose; Keywords = 0x8UL;
              RundownLevel = TraceEventLevel.Verbose; RundownKeywords = 0UL } |]
        Initialize = 
            fun (id, broadcast) -> ({
                HandlerId = id
                Broadcast = broadcast
            } :> obj)
        Subscribe = subscribe
    }
