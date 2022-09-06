module LowLevelDesign.WTrace.Summary.TraceSummary

open System
open System.Collections.Generic
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events.FieldValues

[<AutoOpen>]
module private H =

    let updateCounter<'T> (counter : Dictionary<'T, uint64>) key count =
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


let init debugSymbols ct =
    let state = {
        DebugSymbols = debugSymbols
        Cancellation = ct

        SystemImageBaseAddresses = List<uint64>(200)
        LoadedSystemImages = Dictionary<uint64, ImageInMemory>(200)

        Processes = Dictionary<int32, array<ProcessRecord>>()
        LastUniqueProcessId = 0

        RpcInterfaceProcedureNames = Dictionary<Guid, array<string>>()
        RpcBindingToResolveQueue = Queue<string>()
        RpcModulesParsed = HashSet<string>()

        Counters = {
            FileReadBytes = NumericCounter()
            FileWrittenBytes = NumericCounter()
            TcpReceivedBytes = NumericCounter()
            TcpSentBytes = NumericCounter()
            UdpReceivedBytes = NumericCounter()
            UdpSentBytes = NumericCounter()
            RpcClientCalls = Dictionary<string * Guid * int32, uint64>()
            RpcServerCalls = Dictionary<string * Guid * int32, uint64>()
            DpcCalls = Dictionary<uint64, int32 * float>()
            IsrCalls = Dictionary<uint64, int32 * float>()
        }
    }

    Async.Start (RpcResolver.resolveRpcBindingsAsync state, state.Cancellation)

    state

let processEvent state (TraceEventWithFields (ev, fields)) =
    lock state (fun () ->
        let c = state.Counters

        if ev.EventName = "Process/Start" || ev.EventName = "Process/DCStart" then
            ProcessTree.handleProcessStart state ev fields
        elif ev.EventName = "Process/Stop" then
            ProcessTree.handleProcessExit state ev
        elif ev.EventName = "FileIO/Read" then
            updateCounter c.FileReadBytes ev.Path (getUI64FieldValue fields "ExtraInfo")
        elif ev.EventName = "FileIO/Write" then
            updateCounter c.FileWrittenBytes ev.Path (getUI64FieldValue fields "ExtraInfo")
        elif ev.EventName = "TcpIp/Recv" then
            updateCounter c.TcpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName = "TcpIp/Send" then
            updateCounter c.TcpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName = "TcpIp/RecvIPv6" then
            updateCounter c.TcpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName = "TcpIp/SendIPv6" then
            updateCounter c.TcpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName = "UdpIp/Recv" then
            updateCounter c.UdpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName = "UdpIp/Send" then
            updateCounter c.UdpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName = "UdpIp/RecvIPv6" then
            updateCounter c.UdpReceivedBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName = "UdpIp/SendIPv6" then
            updateCounter c.UdpSentBytes ev.Path (uint64 (getI32FieldValue fields "size"))
        elif ev.EventName = "RPC/ServerCallStart" || ev.EventName = "RPC/ClientCallStart" then
            let binding = getTextFieldValue fields "Binding"
            let interfaceUuid = getGuidFieldValue fields "InterfaceUuid"
            let procNum = getI32FieldValue fields "ProcNum"

            let counter = if ev.EventName = "RPC/ServerCallStart" then c.RpcServerCalls else c.RpcClientCalls
            updateCounter counter (binding, interfaceUuid, procNum) 1UL

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
        elif ev.EventName = "PerfInfo/DPC" || ev.EventName = "PerfInfo/ISR" then
            let routine = getUI64FieldValue fields "Routine"
            match SystemImages.findImage state routine with
            | ValueSome img ->
                let elapsedTime = getF64FieldValue fields "ElapsedTimeMSec"
                let counters = if ev.EventName = "PerfInfo/DPC" then c.DpcCalls else c.IsrCalls
                updateCounterAndElapsedTime counters img.BaseAddress 1 elapsedTime
            | ValueNone ->
                Logger.EtwTracing.TraceWarning (sprintf "Possibly missing ImageLoad events. Address: 0x%x" routine)
    )

let dump state =
    lock state (fun () ->
        if not (ProcessTree.isEmpty state) then
            printTitle "Processes"
            ProcessTree.printProcessTree state

        let c = state.Counters
        if c.FileReadBytes.Count > 0 || c.FileWrittenBytes.Count > 0 then
            printTitle "File I/O"
            c.FileReadBytes.Keys
            |> Seq.append c.FileWrittenBytes.Keys
            |> Seq.distinct
            |> Seq.map(fun p ->
                           let (written, read) = (getCounterValue c.FileWrittenBytes p, getCounterValue c.FileReadBytes p)
                           (p, read + written, written, read))
            |> Seq.sortByDescending (fun (_, total, _, _) -> total)
            |> Seq.iter (fun (path, total, written, read) ->
                            printfn "'%s' Total: %dB, Writes: %dB, Reads: %dB" path total written read)

        dumpNetworkStatistics "TCP/IP" c.TcpReceivedBytes c.TcpSentBytes
        dumpNetworkStatistics "UDP" c.UdpReceivedBytes c.UdpSentBytes


        let printRpcCalls (calls : Dictionary<string * Guid * int32, uint64>) =
            calls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, total) -> total)
            |> Seq.iter (
                fun ((binding, interfaceUuid, procNum), total) ->
                    let path = match state.RpcInterfaceProcedureNames.TryGetValue(interfaceUuid) with
                               | (true, procedures) when procedures.Length > procNum ->
                                   sprintf "%O (%s) [%d]{%s}" interfaceUuid binding procNum procedures[procNum]
                               | _ -> sprintf "%O (%s) [%d]" interfaceUuid binding procNum
                    printfn "%s calls: %d" path total
                )

        if c.RpcClientCalls.Count > 0 then
            printTitle "RPC (clients)"
            printRpcCalls c.RpcClientCalls
        
        if c.RpcServerCalls.Count > 0 then
            printTitle "RPC (servers)"
            printRpcCalls c.RpcServerCalls

        if c.DpcCalls.Count > 0 then
            printTitle "DPC"
            c.DpcCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, (_, t)) -> t)
            |> Seq.iter (fun (baseAddr, (count, time)) ->
                            let img = state.LoadedSystemImages[baseAddr]
                            let time = time.ToString("#,0.000")
                            printfn "'%s', Total: %s ms (%d event(s))" img.FileName time count)

        if c.IsrCalls.Count > 0 then
            printTitle "ISR"
            c.IsrCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, (_, t)) -> t)
            |> Seq.iter (fun (baseAddr, (count, time)) ->
                            let img = state.LoadedSystemImages[baseAddr]
                            let time = time.ToString("#,0.000")
                            printfn "'%s', Total: %s ms (%d event(s))" img.FileName time count)
    )
