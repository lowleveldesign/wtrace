module LowLevelDesign.WTrace.Summary.RpcResolver

open System
open System.ComponentModel
open System.Diagnostics
open System.Threading
open LowLevelDesign.WTrace
open NtApiDotNet.Win32

[<AutoOpen>]
module private H =

    let logger = Logger.Processing

    let resolveRpcServer (state : TraceSummaryState) (srv : RpcServer) =
        let procedures = srv.Procedures |> Seq.map (fun proc -> proc.Name) |> Seq.toArray
        lock state (fun () ->
            logger.TraceVerbose($"[rpc](%d{Thread.CurrentThread.ManagedThreadId}) resolved interface '{srv.InterfaceId}'") // FIXME: remove
            state.RpcInterfaceProcedureNames[srv.InterfaceId] <- procedures
        )

    let resolveEndpoint (state : TraceSummaryState) (endpoint : RpcEndpoint) =
        logger.TraceVerbose($"[rpc](%d{Thread.CurrentThread.ManagedThreadId}) resolving endpoint '%s{endpoint.Endpoint}' - %O{endpoint.InterfaceId}")
        let processInfo = endpoint.GetServerProcess()

        let modules =
            lock state (
                fun () ->
                    try
                        // FIXME: not optimal - we should use data collected from ETW
                        let modules = Process.GetProcessById(processInfo.ProcessId).Modules
                                      |> Seq.cast<ProcessModule>
                                      |> Seq.map (fun m -> m.FileName)
                                      |> Seq.where (fun m -> not (state.RpcModulesParsed.Contains(m))) |> Array.ofSeq
                        modules |> Array.iter (fun m -> state.RpcModulesParsed.Add(m) |> ignore) 
                        modules
                    with
                    | :? Win32Exception as ex ->
                        logger.TraceWarning($"[rpc] ERROR when opening process %d{processInfo.ProcessId}: 0x%x{ex.ErrorCode}")
                        Array.empty
            )

        let (dbgHelpPath, symbolsPath, parserFlags) =
            match state.DebugSymbols with
            | Ignore -> ("", "", RpcServerParserFlags.IgnoreSymbols)
            | UseDbgHelp(dbgHelpPath, symbolsPath) -> (dbgHelpPath, symbolsPath, RpcServerParserFlags.None)

        modules
        |> Seq.collect (
                fun m ->
                    logger.TraceVerbose($"[rcp] parsing module '%s{m}'")
                    RpcServer.ParsePeFile(m, dbgHelpPath, symbolsPath, parserFlags))
        |> Seq.iter (resolveRpcServer state)

    let resolveBindingAsync (state : TraceSummaryState) (binding : string) (taskCount : int32 ref) =
        async {
            try
                logger.TraceVerbose($"[rpc](%d{Thread.CurrentThread.ManagedThreadId}) resolving binding '%s{binding}'")

                match RpcEndpointMapper.QueryEndpointsForBinding(binding, false) with
                | ntresult when ntresult.IsSuccess ->
                    ntresult.Result |> Seq.iter (resolveEndpoint state)
                | ntresult ->
                    logger.TraceWarning($"[rpc](%d{Thread.CurrentThread.ManagedThreadId}) error when resolving binding '%s{binding}': %x{uint32 ntresult.Status}")
            finally
                Interlocked.Decrement(taskCount) |> ignore
        }

let resolveRpcBindingsAsync (state : TraceSummaryState) =
    let tryDequeueBinding () =
        lock state (fun () ->
            if state.RpcBindingToResolveQueue.Count > 0 then
                Some (state.RpcBindingToResolveQueue.Dequeue())
            else None
        )

    async {
        let taskCount = ref 0

        while not state.Cancellation.IsCancellationRequested do
            match tryDequeueBinding () with
            | Some binding when !taskCount < 4 ->
                Interlocked.Increment(taskCount) |> ignore

                Async.Start (resolveBindingAsync state binding taskCount, state.Cancellation)
            | _ -> do! Async.Sleep (TimeSpan.FromMilliseconds(100))
    }
 
