module LowLevelDesign.WTrace.Processing.RpcResolver

open System
open System.Collections.Generic
open System.Threading
open NtApiDotNet.Win32
open LowLevelDesign.WTrace

[<AutoOpen>]
module private H =

    let logger = Logger.Processing

    let rpcBindingToResolveQueue = Queue<string>()
    let rpcModulesParsed = HashSet<string>()

    let taskCount = ref 0

    let resolveRpcServer state (srv : RpcServer) =
        let procedures = srv.Procedures |> Seq.map (fun proc -> proc.Name) |> Seq.toArray
        lock state (fun () ->
            state.RpcInterfaceProcedureNames[srv.InterfaceId] <- procedures
        )

    let resolveEndpoint state (endpoint : RpcEndpoint) =
        let processInfo = endpoint.GetServerProcess()

        let modules =
            lock state (
                fun () ->
                    lock rpcModulesParsed (fun () ->
                        match state.LoadedProcessModules.TryGetValue(processInfo.ProcessId) with
                        | (true, modules) ->
                                let modules = modules |> Seq.where (fun m -> not (rpcModulesParsed.Contains(m))) |> Array.ofSeq
                                modules |> Array.iter (fun m -> rpcModulesParsed.Add(m) |> ignore)
                                modules
                        | (false, _) ->
                            logger.TraceWarning($"[rpc] modules not found for a process %d{processInfo.ProcessId} (did you enable the image handler?)")
                            Array.empty
                    )
            )

        let (dbgHelpPath, symbolsPath, parserFlags) =
            match state.DebugSymbols with
            | Ignore -> ("", "", RpcServerParserFlags.IgnoreSymbols)
            | UseDbgHelp(dbgHelpPath, symbolsPath) -> (dbgHelpPath, symbolsPath, RpcServerParserFlags.None)

        modules
        |> Seq.collect (
                fun m ->
                    logger.TraceVerbose($"[rpc] parsing module '%s{m}'")
                    RpcServer.ParsePeFile(m, dbgHelpPath, symbolsPath, parserFlags))
        |> Seq.iter (resolveRpcServer state)

    let resolveBindingAsync state binding (taskCount : int32 ref) =
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

let isRunning () =
    lock rpcBindingToResolveQueue (fun () ->
        rpcBindingToResolveQueue.Count > 0 || taskCount.Value > 0
    )

let enqueueBindingToResolve binding =
    lock rpcBindingToResolveQueue (
        fun () -> rpcBindingToResolveQueue.Enqueue(binding))

let resolveRpcBindingsAsync state =
    let tryDequeueBinding () =
        lock rpcBindingToResolveQueue (fun () ->
            if rpcBindingToResolveQueue.Count > 0 then
                Some (rpcBindingToResolveQueue.Dequeue())
            else None
        )

    async {
        let! ct = Async.CancellationToken
        while not ct.IsCancellationRequested do
            match tryDequeueBinding () with
            | Some binding when taskCount.Value < 4 ->
                Debug.Assert(binding <> "")
                Interlocked.Increment(taskCount) |> ignore

                Async.Start (resolveBindingAsync state binding taskCount, ct)
            | _ -> do! Async.Sleep (TimeSpan.FromMilliseconds(100))
    }
 
