module LowLevelDesign.WTrace.Processing.ProcessTree

open System
open System.Collections.Generic
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events.FieldValues

[<AutoOpen>]
module private H =

    let mutable lastUniqueProcessId = 0

    let findProcessUniqueId (processes : Dictionary<int32, array<ProcessRecord>>) pid timeStamp =
        let mutable procs = null
        if processes.TryGetValue(pid, &procs) then
            let p = procs
                    |> Array.find (fun p -> p.StartTime <= timeStamp)
            Some p.UniqueProcessId
        else None

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

    let updateTree processMap (tree : Dictionary<int32, ProcessNode>) id =
        let rec findOrCreateProcessNode id =
            match processMap |> Map.find id with
            | { UniqueParentId = Some parentId } ->
                let parentNode = findOrCreateProcessNode parentId
                if not (parentNode.Children.ContainsKey(id)) then
                    let node = { Id = id; Children = Dictionary<_, _>() }
                    parentNode.Children.Add(id, node)
                    node
                else
                    parentNode.Children.[id]
            | _ ->
                match tree.TryGetValue(id) with
                | (true, node) -> node
                | _ ->
                    let node = { Id = id; Children = Dictionary<_, _>() }
                    tree.Add(id, node)
                    node
        findOrCreateProcessNode id |> ignore
        tree


let handleProcessStart state ev fields =
    let imageFileName = getTextFieldValue fields "ImageFileName"
    let parentId = getI32FieldValue fields "ParentID"
    lastUniqueProcessId <- lastUniqueProcessId + 1
    let proc = {
        SystemProcessId = ev.ProcessId
        SystemParentId = parentId
        UniqueProcessId = lastUniqueProcessId
        UniqueParentId = findProcessUniqueId state.Processes parentId ev.TimeStamp
        ProcessName = getProcessName imageFileName
        StartTime = if ev.EventName = "Process/Start" then ev.TimeStamp else DateTime.MinValue
        CommandLine = getTextFieldValue fields "CommandLine"
        ExitTime = DateTime.MaxValue
    }

    match state.Processes.TryGetValue(proc.SystemProcessId) with
    | (true, procs) ->
        Debug.Assert(procs.Length > 0, "[SystemEvents] there should be always at least one process in the list")
        // It may happen that a session started after creating the main session and before the rundown 
        // session started. We can safely skip this process.
        if procs.[0].ExitTime < DateTime.MaxValue then
            state.Processes[proc.SystemProcessId] <- procs |> Array.append [| proc |]
    | (false, _) ->
        state.Processes.Add(proc.SystemProcessId, [| proc |])

let handleProcessExit state ev =
    match state.Processes.TryGetValue(ev.ProcessId) with
    | (true, procs) ->
        match procs with
        | [| |] -> Debug.Assert(false, "[SystemEvents] there should be always at least one process in the list")
        | arr -> arr.[0] <- { arr.[0] with ExitTime = ev.TimeStamp } // the first one is always the running one
    | (false, _) -> ()

let isEmpty state = state.Processes.Count = 0

let printProcessTree state =
    let processMap =
        state.Processes
        |> Seq.map (|KeyValue|)
        |> Seq.collect (fun (k, v) -> v)
        |> Seq.map (fun p -> (p.UniqueProcessId, p))
        |> Map.ofSeq

    let tree =
        processMap
        |> Map.toSeq
        |> Seq.map fst
        |> Seq.fold (updateTree processMap) (Dictionary<int32, ProcessNode>())

    let rec printChildren depth node =
        let proc = processMap |> Map.find node.Id
        let startTime =
            if proc.StartTime <> DateTime.MinValue then sprintf "started at %s" (proc.StartTime.ToString("HH:mm:ss.ffff"))
            else ""
        let exitTime =
            if proc.ExitTime <> DateTime.MaxValue then sprintf "finished at %s" (proc.ExitTime.ToString("HH:mm:ss.ffff"))
            else ""
        printfn "%s├─ %s [%d] %s  %s" ("│ " |> String.replicate depth) proc.ProcessName proc.SystemProcessId startTime exitTime
        node.Children.Values |> Seq.iter (printChildren (depth + 1))

    for n in tree.Values do
        printChildren 0 n

