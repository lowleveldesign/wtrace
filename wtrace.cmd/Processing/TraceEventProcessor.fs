module LowLevelDesign.WTrace.Processing.TraceEventProcessor

open System
open System.Collections.Generic
open System.Diagnostics
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events.FieldValues

[<AutoOpen>]
module private H =

    let logger = Logger.Processing

    let currentProcessPid = Process.GetCurrentProcess().Id

    let isRundown ev = ev.EventName.EndsWith("/DCStart")


let init processFilter debugSymbols ct =
    let state = {
        ProcessFilter = processFilter

        SystemImageBaseAddresses = List<uint64>(200)
        LoadedSystemImages = Dictionary<uint64, ImageInMemory>(200)

        Processes = Dictionary<int32, array<ProcessRecord>>()
        LoadedProcessModules = Dictionary<int32, HashSet<string>>()

        RpcInterfaceProcedureNames = Dictionary<Guid, array<string>>()
        RpcBindingToResolveQueue = Queue<string>()
        RpcModulesParsed = HashSet<string>()
    }

    Async.Start (RpcResolver.resolveRpcBindingsAsync debugSymbols state, ct)

    state

let processAndFilterEvent state (TraceEventWithFields (ev, fields)) =
    lock state (fun () ->
        // will run for all the events
        if ev.EventName = "Process/Start" || ev.EventName = "Process/DCStart" then
            let parentId = getI32FieldValue fields "ParentID"
            match state.ProcessFilter with
            | Everything ->
                ProcessTree.handleProcessStart state ev fields
            | Process (pid, false) when ev.ProcessId = pid ->
                logger.TraceInformation($"[filter] including process %s{ev.ProcessName} (%d{ev.ProcessId})")
                ProcessTree.handleProcessStart state ev fields
            | Process (pid, true) when ev.ProcessId = pid || parentId = pid || (state.Processes.ContainsKey(parentId)) ->
                logger.TraceInformation($"[filter] including process %s{ev.ProcessName} (%d{ev.ProcessId})")
                ProcessTree.handleProcessStart state ev fields
            | _ -> ()
        elif ev.EventName = "Process/Stop" then
            ProcessTree.handleProcessExit state ev
        elif ev.EventName = "Image/Load" || ev.EventName = "Image/DCStart" then
            match state.LoadedProcessModules.TryGetValue(ev.ProcessId) with
            | (true, modules) -> modules.Add(ev.Path) |> ignore
            | _ -> state.LoadedProcessModules.Add(ev.ProcessId, HashSet<string>(Seq.singleton ev.Path))
        elif ev.EventName = "Image/Unload" then
            match state.LoadedProcessModules.TryGetValue(ev.ProcessId) with
            | (true, modules) ->
                modules.Remove(ev.Path) |> ignore
                if modules.Count = 0 then
                    state.LoadedProcessModules.Remove(ev.ProcessId) |> ignore
            | _ -> ()
        elif ev.EventName = "SystemImage/Load" then
            let image = { BaseAddress = getUI64FieldValue fields "ImageBase"
                          ImageSize = getI32FieldValue fields "ImageSize"
                          FileName = ev.Path }
            SystemImages.addImage state image
        elif ev.EventName = "SystemImage/Unload" then
            let baseAddress = getUI64FieldValue fields "ImageBase"
            SystemImages.removeImage state baseAddress

        // should we allow this event?
        let processFilterResult =
            ev.ProcessId <> currentProcessPid && (not (isRundown ev)) &&
                match state.ProcessFilter with
                | Everything -> true
                | _ -> state.Processes.ContainsKey(ev.ProcessId)

        // applies only to filtered events
        if processFilterResult && (ev.EventName = "RPC/ServerCallStart" || ev.EventName = "RPC/ClientCallStart") then
            let binding = getTextFieldValue fields "Binding"
            let interfaceUuid = getGuidFieldValue fields "InterfaceUuid"

            if binding <> "" && not (state.RpcInterfaceProcedureNames.ContainsKey(interfaceUuid)) then
                state.RpcInterfaceProcedureNames.Add(interfaceUuid, Array.empty)
                state.RpcBindingToResolveQueue.Enqueue(binding)

        processFilterResult
    )

