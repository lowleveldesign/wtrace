namespace LowLevelDesign.WTrace

open System
open System.Collections.Generic
open LowLevelDesign.WTrace.Events.FieldValues

module TraceStatistics =

    module private ProcessTree =

        [<AutoOpen>]
        module private H =
            type ProcessRecord = {
                UniqueProcessId : int32
                UniqueParentId : option<int32>
                SystemProcessId : int32
                SystemParentId : int32
                ProcessName : string
                CommandLine : string
                StartTime : DateTime
                ExitTime : DateTime
            }

            type ProcessNode = { 
                Id : int32
                Children : Dictionary<int32, ProcessNode>
            }

            let processes = Dictionary<int32, array<ProcessRecord>>()
            let mutable lastUniqueId = 0

            let findProcessUniqueId pid timeStamp =
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


        let handleProcessStart ev fields =
            let imageFileName = getTextFieldValue fields "ImageFileName"
            let parentId = getI32FieldValue fields "ParentID"
            lastUniqueId <- lastUniqueId + 1
            let proc = {
                SystemProcessId = ev.ProcessId
                SystemParentId = parentId
                UniqueProcessId = lastUniqueId
                UniqueParentId = findProcessUniqueId parentId ev.TimeStamp
                ProcessName = getProcessName imageFileName
                StartTime = if ev.EventName === "Process/Start" then ev.TimeStamp else DateTime.MinValue
                CommandLine = getTextFieldValue fields "CommandLine"
                ExitTime = DateTime.MaxValue
            }

            match processes.TryGetValue(proc.SystemProcessId) with
            | (true, procs) ->
                Debug.Assert(procs.Length > 0, "[SystemEvents] there should be always at least one process in the list")
                // It may happen that a session started after creating the main session and before the rundown 
                // session started. We can safely skip this process.
                if procs.[0].ExitTime < DateTime.MaxValue then
                    processes.[proc.SystemProcessId] <- procs |> Array.append [| proc |]
            | (false, _) ->
                processes.Add(proc.SystemProcessId, [| proc |])

        let handleProcessExit ev =
            match processes.TryGetValue(ev.ProcessId) with
            | (true, procs) ->
                match procs with
                | [| |] -> Debug.Assert(false, "[SystemEvents] there should be always at least one process in the list")
                | arr -> arr.[0] <- { arr.[0] with ExitTime = ev.TimeStamp } // the first one is always the running one
            | (false, _) -> Logger.Tracing.TraceWarning(sprintf "Trying to record exit of a non-existing process: %d" ev.ProcessId)

        let isEmpty () = processes.Count = 0

        let printProcessTree () =
            let processMap =
                processes
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


    [<AutoOpen>]
    module private H =

        type NumericCounter = Dictionary<string, uint64>

        let updateCounter (counter : Dictionary<string, uint64>) key count =
            match counter.TryGetValue(key) with
            | (true, n) -> counter.[key] <- n + count
            | (false, _) -> counter.Add(key, count)

        let updateCounterAndElapsedTime (counters : Dictionary<'T, int32 * float>) key count elapsedTime =
            match counters.TryGetValue(key) with
            | (false, _) ->
                counters.Add(key, (count, elapsedTime))
            | (true, (totalCount, totalElapsedTime)) ->
                counters.[key] <- (totalCount + count, totalElapsedTime + elapsedTime)

        let printTitle (title : string) =
            let separator = "--------------------------------"
            let space = Math.Max(0, (separator.Length - title.Length) / 2)
            printfn ""
            printfn "%s" separator
            printfn "%s%s" (" " |> String.replicate space) title
            printfn "%s" separator

        let getCounterValue (counter : Dictionary<string, uint64>) key =
            match counter.TryGetValue(key) with
            | (true, n) -> n
            | _ -> 0UL

        let dumpNetworkStatistics title (networkReceivedBytes : NumericCounter) (networkSentBytes : NumericCounter) =
            if networkReceivedBytes.Count > 0 || networkSentBytes.Count > 0 then
                printTitle title
                networkSentBytes.Keys
                |> Seq.append networkReceivedBytes.Keys
                |> Seq.distinct
                |> Seq.map(fun p ->
                               let (sent, received) = (getCounterValue networkSentBytes p, getCounterValue networkReceivedBytes p)
                               (p, received + sent, sent, received))
                |> Seq.sortByDescending (fun (_, total, _, _) -> total)
                |> Seq.iter (fun (path, total, sent, received) ->
                                printfn "%s Total: %dB, Sent: %dB, Received: %dB" path total sent received)

        type ImageInMemory = {
            BaseAddress : uint64
            ImageSize : int32
            FileName : string
        }

        let fileReadBytes = NumericCounter()
        let fileWrittenBytes = NumericCounter()
        let tcpReceivedBytes = NumericCounter()
        let tcpSentBytes = NumericCounter()
        let udpReceivedBytes = NumericCounter()
        let udpSentBytes = NumericCounter()
        let rpcCalls = NumericCounter()
        let dpcCalls = Dictionary<uint64, int32 * float>()
        let isrCalls = Dictionary<uint64, int32 * float>()

    module private SystemImages =

        let baseAddresses = List<uint64>(200)
        let loadedImages = Dictionary<uint64, ImageInMemory>(200)

        let addImage image =
            let ind = baseAddresses.BinarySearch(image.BaseAddress)
            if ind < 0 then
                baseAddresses.Insert(~~~ind, image.BaseAddress)
                loadedImages.Add(image.BaseAddress, image)
            else
                Logger.Tracing.TraceWarning (sprintf "Problem when adding image data: 0x%x - it is already added." image.BaseAddress)

        let removeImage baseAddress =
            let ind = baseAddresses.BinarySearch(baseAddress)
            if ind >= 0 then
                baseAddresses.RemoveAt(ind)
                loadedImages.Remove(baseAddress) |> ignore
            else
                Logger.Tracing.TraceWarning (sprintf "Problem when disposing image data: the image 0x%x could not be found." baseAddress)

        let findImage address =
            let tryFindingModule address =
                let ind = baseAddresses.BinarySearch(address)
                if ind < 0 then
                    let ind = ~~~ind
                    if ind = 0 then ValueNone
                    else ValueSome (ind - 1)
                else ValueSome ind

            match tryFindingModule address with
            | ValueNone -> ValueNone
            | ValueSome ind ->
                let baseAddress = baseAddresses.[ind]
                match loadedImages.TryGetValue(baseAddress) with
                | (false, _) ->
                    Debug.Assert(false, sprintf "Missing address in the loadedImages dictionary (0x%x)" baseAddress)
                    ValueNone
                | (true, image) ->
                    if address - baseAddress > uint64 image.ImageSize then
                        ValueNone
                    else ValueSome image


    let processEvent (TraceEventWithFields (ev, fields)) =
        if ev.EventName === "Process/Start" || ev.EventName === "Process/DCStart" then
            ProcessTree.handleProcessStart ev fields
        elif ev.EventName === "Process/Stop" then
            ProcessTree.handleProcessExit ev
        elif ev.EventName === "FileIO/Read" then
            updateCounter fileReadBytes ev.Path (getUI64FieldValue fields "ExtraInfo")
        elif ev.EventName === "FileIO/Write" then
            updateCounter fileWrittenBytes ev.Path (getUI64FieldValue fields "ExtraInfo")
        elif ev.EventName === "TcpIp/Recv" then
            updateCounter tcpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName === "TcpIp/Send" then
            updateCounter tcpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName === "TcpIp/RecvIPv6" then
            updateCounter tcpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName === "TcpIp/SendIPv6" then
            updateCounter tcpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName === "UdpIp/Recv" then
            updateCounter udpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName === "UdpIp/Send" then
            updateCounter udpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName === "UdpIp/RecvIPv6" then
            updateCounter udpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName === "UdpIp/SendIPv6" then
            updateCounter udpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName.StartsWith("RPC/", StringComparison.Ordinal) then
            match rpcCalls.TryGetValue(ev.Path) with
            | (true, n) -> rpcCalls.[ev.Path] <- n + 1UL
            | (false, _) -> rpcCalls.Add(ev.Path, 1UL)
        elif ev.EventName === "SystemImage/Load" then
            let image = { BaseAddress = getUI64FieldValue fields "ImageBase"
                          ImageSize = getI32FieldValue fields "ImageSize"
                          FileName = ev.Path }
            SystemImages.addImage image
        elif ev.EventName === "SystemImage/Unload" then
            let baseAddress = getUI64FieldValue fields "ImageBase"
            SystemImages.removeImage baseAddress
        elif ev.EventName === "PerfInfo/DPC" || ev.EventName === "PerfInfo/ISR" then
            let routine = getUI64FieldValue fields "Routine"
            match SystemImages.findImage routine with
            | ValueSome img ->
                let elapsedTime = getF64FieldValue fields "ElapsedTimeMSec"
                let counters = if ev.EventName === "PerfInfo/DPC" then dpcCalls else isrCalls
                updateCounterAndElapsedTime counters img.BaseAddress 1 elapsedTime
            | ValueNone ->
                Logger.EtwTracing.TraceWarning (sprintf "Possibly missing ImageLoad events. Address: 0x%x" routine)

    let dumpStatistics () =
        if not (ProcessTree.isEmpty ()) then
            printTitle "Processes"
            ProcessTree.printProcessTree ()

        if fileReadBytes.Count > 0 || fileWrittenBytes.Count > 0 then
            printTitle "File I/O"
            fileReadBytes.Keys
            |> Seq.append fileWrittenBytes.Keys
            |> Seq.distinct
            |> Seq.map(fun p ->
                           let (written, read) = (getCounterValue fileWrittenBytes p, getCounterValue fileReadBytes p)
                           (p, read + written, written, read))
            |> Seq.sortByDescending (fun (_, total, _, _) -> total)
            |> Seq.iter (fun (path, total, written, read) ->
                            printfn "'%s' Total: %dB, Writes: %dB, Reads: %dB" path total written read)

        dumpNetworkStatistics "TCP/IP" tcpReceivedBytes tcpSentBytes
        dumpNetworkStatistics "UDP" udpReceivedBytes udpSentBytes

        if rpcCalls.Count > 0 then
            printTitle "RPC"
            rpcCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, total) -> total)
            |> Seq.iter (fun (path, total) -> printfn "%s calls: %d" path total)

        if dpcCalls.Count > 0 then
            printTitle "DPC"
            dpcCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, (_, t)) -> t)
            |> Seq.iter (fun (baseAddr, (count, time)) ->
                            let img = SystemImages.loadedImages.[baseAddr]
                            let time = time.ToString("#,0.000")
                            printfn "'%s', Total: %s ms (%d event(s))" img.FileName time count)

        if isrCalls.Count > 0 then
            printTitle "ISR"
            isrCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, (_, t)) -> t)
            |> Seq.iter (fun (baseAddr, (count, time)) ->
                            let img = SystemImages.loadedImages.[baseAddr]
                            let time = time.ToString("#,0.000")
                            printfn "'%s', Total: %s ms (%d event(s))" img.FileName time count)

