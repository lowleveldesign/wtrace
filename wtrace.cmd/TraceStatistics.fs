namespace LowLevelDesign.WTrace

open System
open System.Collections.Generic
open LowLevelDesign.WTrace.Events
open LowLevelDesign.WTrace.Events.FieldValues

module TraceStatistics =

    [<AutoOpen>]
    module private H =

        type ImageInMemory = {
            BaseAddress : uint64
            ImageSize : int32
            FileName : string
        }

        let fileReadBytes = Dictionary<string, int32>()
        let fileWrittenBytes = Dictionary<string, int32>()
        let networkReceivedBytes = Dictionary<string, int32>()
        let networkSentBytes = Dictionary<string, int32>()
        let rpcCalls = Dictionary<string, int32>()
        let dpcCalls = Dictionary<uint64, int32 * float>()
        let isrCalls = Dictionary<uint64, int32 * float>()

        let updateCounter (counter : Dictionary<string, int32>) key count =
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

        let getCounterValue (counter : Dictionary<string, int32>) key =
            match counter.TryGetValue(key) with
            | (true, n) -> n
            | _ -> 0

    module private SystemImages =

        let baseAddresses = List<uint64>(200)
        let loadedImages = Dictionary<uint64, ImageInMemory>(200)

        let addImage image =
            let ind = baseAddresses.BinarySearch(image.BaseAddress)
            if ind < 0 then
                baseAddresses.Insert(~~~ind, image.BaseAddress)
                loadedImages.Add(image.BaseAddress, image)
            else
                Logger.Tracing.TraceWarning $"Problem when adding image data: 0x{image.BaseAddress:X} - it is already added."

        let removeImage baseAddress =
            let ind = baseAddresses.BinarySearch(baseAddress)
            if ind >= 0 then
                baseAddresses.RemoveAt(ind)
                loadedImages.Remove(baseAddress) |> ignore
            else
                Logger.Tracing.TraceWarning $"Problem when disposing image data: the image 0x{baseAddress:X} could not be found."

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
                    Debug.Assert(false, $"Missing address in the loadedImages dictionary (0x{baseAddress:X})")
                    ValueNone
                | (true, image) ->
                    if address - baseAddress > uint64 image.ImageSize then
                        ValueNone
                    else ValueSome image


    let processEvent (TraceEventWithFields (ev, fields)) =
        if ev.EventName === "FileIO/Read" then
            updateCounter fileReadBytes ev.Path (getI32FieldValue fields "ExtraInfo")
        elif ev.EventName === "FileIO/Write" then
            updateCounter fileWrittenBytes ev.Path (getI32FieldValue fields "ExtraInfo")
        elif ev.EventName === "TcpIp/Recv" then
            updateCounter networkReceivedBytes ev.Path (getI32FieldValue fields "size")
        elif ev.EventName === "TcpIp/Send" then
            updateCounter networkSentBytes ev.Path (getI32FieldValue fields "size")
        elif ev.EventName === "TcpIp/RecvIPv6" then
            updateCounter networkReceivedBytes ev.Path (getI32FieldValue fields "size")
        elif ev.EventName === "TcpIp/SendIPv6" then
            updateCounter networkSentBytes ev.Path (getI32FieldValue fields "size")
        elif ev.EventName.StartsWith("RPC/", StringComparison.Ordinal) then
            match rpcCalls.TryGetValue(ev.Path) with
            | (true, n) -> rpcCalls.[ev.Path] <- n + 1
            | (false, _) -> rpcCalls.Add(ev.Path, 1)
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
                Logger.EtwTracing.TraceWarning $"Possibly missing ImageLoad events. Address: 0x{routine:X}"

    let dumpStatistics () =
        if fileReadBytes.Count > 0 || fileWrittenBytes.Count > 0 then
            printTitle "File I/O"
            fileReadBytes.Keys
            |> Seq.append fileWrittenBytes.Keys
            |> Seq.distinct
            |> Seq.map(fun p ->
                           let (written, read) = (getCounterValue fileWrittenBytes p, getCounterValue fileReadBytes p)
                           (p, read + written, written, read))
            |> Seq.sortByDescending (fun (_, total, _, _) -> total)
            |> Seq.iter (fun (path, total, written, read) -> printfn $"'%s{path}' T: %d{total} b, W: %d{written} b, R: %d{read} b")

        if networkReceivedBytes.Count > 0 || networkSentBytes.Count > 0 then
            printTitle "TCP/IP"
            networkSentBytes.Keys
            |> Seq.append networkReceivedBytes.Keys
            |> Seq.distinct
            |> Seq.map(fun p ->
                           let (sent, received) = (getCounterValue networkSentBytes p, getCounterValue networkReceivedBytes p)
                           (p, received + sent, sent, received))
            |> Seq.sortByDescending (fun (_, total, _, _) -> total)
            |> Seq.iter (fun (path, total, sent, received) -> printfn $"%s{path} T: %d{total} b, S: %d{sent} b, R: %d{received} b")

        if rpcCalls.Count > 0 then
            printTitle "RPC"
            rpcCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, total) -> total)
            |> Seq.iter (fun (path, total) -> printfn $"%s{path} calls: %d{total}")

        if dpcCalls.Count > 0 then
            printTitle "DPC"
            dpcCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, (_, t)) -> t)
            |> Seq.iter (fun (baseAddr, (count, time)) ->
                            let img = SystemImages.loadedImages.[baseAddr]
                            let time = time.ToString("#,0.000")
                            printfn $"'{img.FileName}', total: {time}ms ({count} event(s))")

        if isrCalls.Count > 0 then
            printTitle "ISR"
            isrCalls
            |> Seq.map (|KeyValue|)
            |> Seq.sortByDescending (fun (_, (_, t)) -> t)
            |> Seq.iter (fun (baseAddr, (count, time)) ->
                            let img = SystemImages.loadedImages.[baseAddr]
                            let time = time.ToString("#,0.000")
                            printfn $"'{img.FileName}', total: {time}ms ({count} event(s))")

