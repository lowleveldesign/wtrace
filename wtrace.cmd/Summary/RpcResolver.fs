module LowLevelDesign.WTrace.Summary.RpcResolver

open System
open System.ComponentModel
open System.Diagnostics
open System.Threading
open NtApiDotNet.Win32
open PInvoke
open LowLevelDesign.WTrace
open System.Runtime.InteropServices

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
                    let pid = processInfo.ProcessId
                    let accessMask = Kernel32.ACCESS_MASK(Kernel32.ProcessAccess.PROCESS_QUERY_INFORMATION ||| Kernel32.ProcessAccess.PROCESS_VM_READ)
                    use processHandle = Kernel32.OpenProcess(accessMask, false, pid)

                    if not processHandle.IsInvalid then
                        match getModuleHandles (processHandle.DangerousGetHandle()) with
                        | Ok moduleHandles ->
                            let modules = moduleHandles
                                          |> Seq.choose (getModuleName (processHandle.DangerousGetHandle()))
                                          |> Seq.where (fun m -> not (state.RpcModulesParsed.Contains(m))) |> Array.ofSeq
                            modules |> Array.iter (fun m -> state.RpcModulesParsed.Add(m) |> ignore) 
                            modules
                        | Error err -> 
                            logger.TraceWarning($"[rpc] error when querying process %d{pid} modules: %x{err}")
                            Array.empty
                    else
                        let err = Marshal.GetLastWin32Error()
                        logger.TraceWarning($"[rpc] error when opening process %d{pid}: %x{err}")
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
 
