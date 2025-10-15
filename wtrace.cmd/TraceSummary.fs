module LowLevelDesign.WTrace.TraceSummary

open System
open System.Collections.Generic
open LowLevelDesign.WTrace.Processing

[<AutoOpen>]
module private H =

    let printTitle (title : string) =
        let separator = "--------------------------------"
        let space = Math.Max(0, (separator.Length - title.Length) / 2)
        eprintfn ""
        eprintfn "%s" separator
        eprintfn "%s%s" (" " |> String.replicate space) title
        eprintfn "%s" separator

    let getCounterValue (counter : TraceCounters.NumericCounter) key =
        match counter.TryGetValue(key) with
        | (true, n) -> n
        | _ -> 0UL

    let dumpNetworkStatistics title (networkReceivedBytes : TraceCounters.NumericCounter) (networkSentBytes : TraceCounters.NumericCounter) =
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
                            eprintfn "%s Total: %dB, Sent: %dB, Received: %dB" path total sent received)


let dump traceState (counters : TraceCounters.Counters) =
    lock traceState (fun () ->
        if not (ProcessTree.isEmpty traceState) then
            printTitle "Processes"
            ProcessTree.printProcessTree traceState

        if counters.FileReadBytes.Count > 0 || counters.FileWrittenBytes.Count > 0 then
            printTitle "File I/O"
            counters.FileReadBytes.Keys
            |> Seq.append counters.FileWrittenBytes.Keys
            |> Seq.distinct
            |> Seq.map(fun p ->
                           let (written, read) = (getCounterValue counters.FileWrittenBytes p, getCounterValue counters.FileReadBytes p)
                           (p, read + written, written, read))
            |> Seq.sortByDescending (fun (_, total, _, _) -> total)
            |> Seq.iter (fun (path, total, written, read) ->
                            eprintfn "'%s' Total: %dB, Writes: %dB, Reads: %dB" path total written read)

        dumpNetworkStatistics "TCP/IP" counters.TcpReceivedBytes counters.TcpSentBytes
        dumpNetworkStatistics "UDP" counters.UdpReceivedBytes counters.UdpSentBytes


        let printRpcCalls (calls : Dictionary<string * Guid * int32, uint64>) =
            calls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, total) -> total)
            |> Seq.iter (
                fun ((binding, interfaceUuid, procNum), total) ->
                    let path = match traceState.RpcInterfaceProcedureNames.TryGetValue(interfaceUuid) with
                               | (true, procedures) when procedures.Length > procNum ->
                                   sprintf "%O (%s) [%d]{%s}" interfaceUuid binding procNum procedures[procNum]
                               | _ -> sprintf "%O (%s) [%d]" interfaceUuid binding procNum
                    eprintfn "%s calls: %d" path total
                )

        if counters.RpcClientCalls.Count > 0 then
            printTitle "RPC (client calls)"
            printRpcCalls counters.RpcClientCalls
        
        if counters.RpcServerCalls.Count > 0 then
            printTitle "RPC (server calls)"
            printRpcCalls counters.RpcServerCalls

        if counters.DpcCalls.Count > 0 then
            printTitle "DPC"
            counters.DpcCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, (_, t)) -> t)
            |> Seq.iter (fun (baseAddr, (count, time)) ->
                            let img = traceState.LoadedSystemImages[baseAddr]
                            let time = time.ToString("#,0.000")
                            eprintfn "'%s', Total: %s ms (%d event(s))" img.FileName time count)

        if counters.IsrCalls.Count > 0 then
            printTitle "ISR"
            counters.IsrCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, (_, t)) -> t)
            |> Seq.iter (fun (baseAddr, (count, time)) ->
                            let img = traceState.LoadedSystemImages[baseAddr]
                            let time = time.ToString("#,0.000")
                            eprintfn "'%s', Total: %s ms (%d event(s))" img.FileName time count)
    )
