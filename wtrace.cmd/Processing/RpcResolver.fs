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

        let getModuleHandles processHandle =
            let moduleHandles : nativeint[] = Array.zeroCreate 100
            let mutable neededBytes = 0
            if Psapi.EnumProcessModulesEx(processHandle, moduleHandles, moduleHandles.Length * sizeof<nativeint>, 
                                          &neededBytes, Psapi.EnumProcessModulesFlags.LIST_MODULES_DEFAULT) then
                
                let neededTableLength = neededBytes / sizeof<nativeint>
                if neededTableLength > moduleHandles.Length then
                    let moduleHandles : nativeint[] = Array.zeroCreate neededTableLength
                    if Psapi.EnumProcessModulesEx(processHandle, moduleHandles, moduleHandles.Length * sizeof<nativeint>, 
                                                  &neededBytes, Psapi.EnumProcessModulesFlags.LIST_MODULES_DEFAULT) then
                        Ok moduleHandles
                    else Error (Marshal.GetLastWin32Error())
                else Ok (Array.sub moduleHandles 0 neededTableLength)
            else Error (Marshal.GetLastWin32Error())

        let getModuleName processHandle moduleHandle =
            let name : char[] = Array.zeroCreate Kernel32.MAX_PATH
            match Psapi.GetModuleFileNameEx(processHandle, moduleHandle, name, name.Length) with
            | 0 -> None
            | cnt -> Some (System.String(name, 0, cnt))

        let modules =
            lock state (
                fun () ->
                    match state.LoadedProcessModules.TryGetValue(processInfo.ProcessId) with
                    | (true, modules) ->
                            let modules = modules |> Seq.where (fun m -> not (state.RpcModulesParsed.Contains(m))) |> Array.ofSeq
                            modules |> Array.iter (fun m -> state.RpcModulesParsed.Add(m) |> ignore) 
                            modules
                    | (false, _) ->
                        logger.TraceWarning($"[rpc] modules not found for a process %d{processInfo.ProcessId}")
                        Array.empty
            )

        let (dbgHelpPath, symbolsPath, parserFlags) =
            match debugSymbols with
            | Ignore -> ("", "", RpcServerParserFlags.IgnoreSymbols)
            | UseDbgHelp(dbgHelpPath, symbolsPath) -> (dbgHelpPath, symbolsPath, RpcServerParserFlags.None)

        modules
        |> Seq.collect (
                fun m ->
                    logger.TraceVerbose($"[rcp] parsing module '%s{m}'")
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

let resolveRpcBindingsAsync debugSymbols (ct : CancellationToken) state =
    let tryDequeueBinding () =
        lock state (fun () ->
            if state.RpcBindingToResolveQueue.Count > 0 then
                Some (state.RpcBindingToResolveQueue.Dequeue())
            else None
        )

    async {
        let taskCount = ref 0

        while not ct.IsCancellationRequested do
            match tryDequeueBinding () with
            | Some binding when !taskCount < 4 ->
                Interlocked.Increment(taskCount) |> ignore

                Async.Start (resolveBindingAsync debugSymbols state binding taskCount, ct)
            | _ -> do! Async.Sleep (TimeSpan.FromMilliseconds(100))
    }
 
