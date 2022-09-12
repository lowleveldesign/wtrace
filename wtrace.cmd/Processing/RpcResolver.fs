module LowLevelDesign.WTrace.Processing.RpcResolver

open System
open System.Threading
open NtApiDotNet.Win32
open PInvoke
open LowLevelDesign.WTrace
open System.Runtime.InteropServices

[<AutoOpen>]
module private H =

    let logger = Logger.Processing

    let resolveRpcServer state (srv : RpcServer) =
        let procedures = srv.Procedures |> Seq.map (fun proc -> proc.Name) |> Seq.toArray
        lock state (fun () ->
            logger.TraceVerbose($"[rpc](%d{Thread.CurrentThread.ManagedThreadId}) resolved interface '{srv.InterfaceId}'") // FIXME: remove
            state.RpcInterfaceProcedureNames[srv.InterfaceId] <- procedures
        )

    let resolveEndpoint debugSymbols state (endpoint : RpcEndpoint) =
        logger.TraceVerbose($"[rpc](%d{Thread.CurrentThread.ManagedThreadId}) resolving endpoint '%s{endpoint.Endpoint}' - %O{endpoint.InterfaceId}")
        let processInfo = endpoint.GetServerProcess()

        let modules =
            lock state (
                fun () ->
                    match state.LoadedProcessModules.TryGetValue(processInfo.ProcessId) with
                    | (true, modules) ->
                            let modules = modules |> Seq.where (fun m -> not (state.RpcModulesParsed.Contains(m))) |> Array.ofSeq
                            modules |> Array.iter (fun m -> state.RpcModulesParsed.Add(m) |> ignore) 
                            modules
                    | (false, _) ->
                        logger.TraceWarning($"[rpc] modules not found for a process %d{processInfo.ProcessId} (did you enable the image handler?)")
                        Array.empty
            )

        let (dbgHelpPath, symbolsPath, parserFlags) =
            match debugSymbols with
            | Ignore -> ("", "", RpcServerParserFlags.IgnoreSymbols)
            | UseDbgHelp(dbgHelpPath, symbolsPath) -> (dbgHelpPath, symbolsPath, RpcServerParserFlags.None)

        modules
        |> Seq.collect (
                fun m ->
                    logger.TraceVerbose($"[rpc] parsing module '%s{m}'")
                    RpcServer.ParsePeFile(m, dbgHelpPath, symbolsPath, parserFlags))
        |> Seq.iter (resolveRpcServer state)

    let resolveBindingAsync debugSymbols state binding (taskCount : int32 ref) =
        async {
            try
                logger.TraceVerbose($"[rpc](%d{Thread.CurrentThread.ManagedThreadId}) resolving binding '%s{binding}'")

                match RpcEndpointMapper.QueryEndpointsForBinding(binding, false) with
                | ntresult when ntresult.IsSuccess ->
                    ntresult.Result |> Seq.iter (resolveEndpoint debugSymbols state)
                | ntresult ->
                    logger.TraceWarning($"[rpc](%d{Thread.CurrentThread.ManagedThreadId}) error when resolving binding '%s{binding}': %x{uint32 ntresult.Status}")
            finally
                Interlocked.Decrement(taskCount) |> ignore
        }

let resolveRpcBindingsAsync debugSymbols state =
    let tryDequeueBinding () =
        lock state (fun () ->
            if state.RpcBindingToResolveQueue.Count > 0 then
                Some (state.RpcBindingToResolveQueue.Dequeue())
            else None
        )

    async {
        let taskCount = ref 0

        let! ct = Async.CancellationToken
        while not ct.IsCancellationRequested do
            match tryDequeueBinding () with
            | Some binding when taskCount.Value < 4 ->
                Debug.Assert(binding <> "")
                Interlocked.Increment(taskCount) |> ignore

                Async.Start (resolveBindingAsync debugSymbols state binding taskCount, ct)
            | _ -> do! Async.Sleep (TimeSpan.FromMilliseconds(100))
    }
 
