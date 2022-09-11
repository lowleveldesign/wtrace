module LowLevelDesign.WTrace.Processing.TraceEventPostprocessing

open System
open System.Collections.Generic
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events.FieldValues

let init debugSymbols ct =
    let state = {
        SystemImageBaseAddresses = List<uint64>(200)
        LoadedSystemImages = Dictionary<uint64, ImageInMemory>(200)

        Processes = Dictionary<int32, array<ProcessRecord>>()
        LoadedProcessModules = Dictionary<int32, HashSet<string>>()
        LastUniqueProcessId = 0

        RpcInterfaceProcedureNames = Dictionary<Guid, array<string>>()
        RpcBindingToResolveQueue = Queue<string>()
        RpcModulesParsed = HashSet<string>()

    }

    Async.Start (RpcResolver.resolveRpcBindingsAsync debugSymbols ct state, ct)

    state

let processUnfilteredEvent state (TraceEventWithFields (ev, fields)) =
    lock state (fun () ->
        if ev.EventName = "Process/Start" || ev.EventName = "Process/DCStart" then
            ProcessTree.handleProcessStart state ev fields
        elif ev.EventName = "Process/Stop" then
            ProcessTree.handleProcessExit state ev
        elif ev.EventName = "Image/Load" || ev.EventName = "Image/Loaded" then
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
        elif ev.EventName = "RPC/ServerCallStart" || ev.EventName = "RPC/ClientCallStart" then
            let binding = getTextFieldValue fields "Binding"
            let interfaceUuid = getGuidFieldValue fields "InterfaceUuid"
            let procNum = getI32FieldValue fields "ProcNum"

            if not (state.RpcInterfaceProcedureNames.ContainsKey(interfaceUuid)) then
                state.RpcInterfaceProcedureNames.Add(interfaceUuid, Array.empty)
                state.RpcBindingToResolveQueue.Enqueue(binding)
        elif ev.EventName = "SystemImage/Load" then
            let image = { BaseAddress = getUI64FieldValue fields "ImageBase"
                          ImageSize = getI32FieldValue fields "ImageSize"
                          FileName = ev.Path }
            SystemImages.addImage state image
        elif ev.EventName = "SystemImage/Unload" then
            let baseAddress = getUI64FieldValue fields "ImageBase"
            SystemImages.removeImage state baseAddress
        )

