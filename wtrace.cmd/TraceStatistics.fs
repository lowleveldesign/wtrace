namespace LowLevelDesign.WTrace

open System
open System.Collections.Generic

type TraceStatistics = {
    FileReadBytes : Dictionary<string, int32>
    FileWrittenBytes : Dictionary<string, int32>
    NetworkReceivedBytes : Dictionary<string, int32>
    NetworkSentBytes : Dictionary<string, int32>
    RpcCalls : Dictionary<string, int32>
}

module TraceStatistics =

    [<AutoOpen>]
    module private H =
        let updateCounter (counter : Dictionary<string, int32>) key field =
            let count = match field with
                        | Some { FieldValue = n } -> Int32.Parse(n)
                        | None ->
                             Debug.Assert(false, sprintf "[stats] Missing required field for property %s" key)
                             0
            match counter.TryGetValue(key) with
            | (true, n) -> counter.[key] <- n + count
            | (false, _) -> counter.Add(key, count)

        let (===) a b = String.Equals(a, b, StringComparison.Ordinal)

        let printTitle (title : string) =
            let separator = "--------------------------------"
            let space = Math.Max(0, (separator.Length - title.Length) / 2)
            printfn ""
            printfn "%s" separator
            printfn "%s%s" (" " |> String.replicate space) title
            printfn "%s" separator

        let getCounterValue (counter : Dictionary<string, int32>) key =
            match counter.TryGetValue(key) with
            | (true, n) -> n
            | _ -> 0

    let create () = {
        FileReadBytes = Dictionary<string, int32>()
        FileWrittenBytes = Dictionary<string, int32>()
        NetworkReceivedBytes = Dictionary<string, int32>()
        NetworkSentBytes = Dictionary<string, int32>()
        RpcCalls = Dictionary<string, int32>()
    }

    let processEvent stats (TraceEventWithFields (ev, fields)) =
        if ev.EventName === "FileIO/Read" then
            updateCounter stats.FileReadBytes ev.Path (fields |> Array.tryFind (fun fld -> fld.FieldName === "ExtraInfo"))
        elif ev.EventName === "FileIO/Write" then
            updateCounter stats.FileWrittenBytes ev.Path (fields |> Array.tryFind (fun fld -> fld.FieldName === "ExtraInfo"))
        elif ev.EventName === "TcpIp/Recv" then
            updateCounter stats.NetworkReceivedBytes ev.Path (fields |> Array.tryFind (fun fld -> fld.FieldName === "size"))
        elif ev.EventName === "TcpIp/Send" then
            updateCounter stats.NetworkSentBytes ev.Path (fields |> Array.tryFind (fun fld -> fld.FieldName === "size"))
        elif ev.EventName === "TcpIp/RecvIPv6" then
            updateCounter stats.NetworkReceivedBytes ev.Path (fields |> Array.tryFind (fun fld -> fld.FieldName === "size"))
        elif ev.EventName === "TcpIp/SendIPv6" then
            updateCounter stats.NetworkSentBytes ev.Path (fields |> Array.tryFind (fun fld -> fld.FieldName === "size"))
        elif ev.EventName.StartsWith("RPC/", StringComparison.Ordinal) then
            match stats.RpcCalls.TryGetValue(ev.Path) with
            | (true, n) -> stats.RpcCalls.[ev.Path] <- n + 1
            | (false, _) -> stats.RpcCalls.Add(ev.Path, 1)

    let dumpStatistics stats =
        if stats.FileReadBytes.Count > 0 || stats.FileWrittenBytes.Count > 0 then
            printTitle "File I/O"
            stats.FileReadBytes.Keys
            |> Seq.append stats.FileWrittenBytes.Keys
            |> Seq.distinct
            |> Seq.map(fun p ->
                           let (written, read) = (getCounterValue stats.FileWrittenBytes p, getCounterValue stats.FileReadBytes p)
                           (p, read + written, written, read))
            |> Seq.sortByDescending (fun (_, total, _, _) -> total)
            |> Seq.iter (fun (path, total, written, read) -> printfn "'%s' T: %d b, W: %d b, R: %d b" path total written read)

        if stats.NetworkReceivedBytes.Count > 0 || stats.NetworkSentBytes.Count > 0 then
            printTitle "TCP/IP"
            stats.NetworkSentBytes.Keys
            |> Seq.append stats.NetworkReceivedBytes.Keys
            |> Seq.distinct
            |> Seq.map(fun p ->
                           let (sent, received) = (getCounterValue stats.NetworkSentBytes p, getCounterValue stats.NetworkReceivedBytes p)
                           (p, received + sent, sent, received))
            |> Seq.sortByDescending (fun (_, total, _, _) -> total)
            |> Seq.iter (fun (path, total, sent, received) -> printfn "%s T: %d b, S: %d b, R: %d b" path total sent received)

        if stats.RpcCalls.Count > 0 then
            printTitle "RPC"
            stats.RpcCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, total) -> total)
            |> Seq.iter (fun (path, total) -> printfn "%s calls: %d" path total)

