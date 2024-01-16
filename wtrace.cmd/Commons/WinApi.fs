module LowLevelDesign.WTrace.WinApi

open System
open FSharp.NativeInterop
open System.Runtime.InteropServices
open Windows.Win32
open Windows.Win32.Foundation
open Windows.Win32.Security
open Windows.Win32.System.Threading
open System.ComponentModel

let kernelProviderId = Guid(int32 0x9e814aad, int16 0x3204, int16 0x11d2, byte 0x9a, byte 0x82, byte 0x00, byte 0x60, byte 0x08, byte 0xa8, byte 0x69, byte 0x39);

let eventStatusUndefined = Int32.MaxValue

let ntStatusNamesMap =
    NtStatus.GetValues(typedefof<NtStatus>)
    |> Seq.cast<obj>
    |> Seq.map (fun v -> v :?> uint32, NtStatus.GetName(typedefof<NtStatus>, v).Substring("STATUS_".Length))
    |> Seq.distinctBy (fun (v, n) -> v)
    |> Map.ofSeq
    |> Map.add (uint32 eventStatusUndefined) ""

let getNtStatusDesc (n : int32) =
    match ntStatusNamesMap |> Map.tryFind (uint32 n) with
    | None -> sprintf "0x%X" n
    | Some s -> s

let CheckResultBool s b = 
    if b then Ok () else Error (sprintf "[%s] %s" s (Win32Exception().Message))

let CheckResultBOOL s b = 
    if b = BOOL(true) then Ok () else Error (sprintf "[%s] %s" s (Win32Exception().Message))

let CheckResultHandle s (h : HANDLE) =
    if h = HANDLE.INVALID_HANDLE_VALUE then Error (sprintf "[%s] %s" s (Win32Exception().Message))
    else Ok h

let Win32ErrorMessage (err : int) = 
    Win32Exception(err).Message

let startProcessSuspended (args : seq<string>) spawnNewConsole =
    let cmdline = String.Join(" ", args)
    use cmdlinePtr = fixed cmdline

    result {
        let pi = NativePtr.stackalloc<PROCESS_INFORMATION> 1
        let si = NativePtr.stackalloc<STARTUPINFOW> 1
        NativePtr.write si (STARTUPINFOW(cb = uint32 (Marshal.SizeOf<STARTUPINFOW>())))

        let flags = PROCESS_CREATION_FLAGS.CREATE_SUSPENDED ||| (
            if spawnNewConsole then PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE else (
                Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint32, PROCESS_CREATION_FLAGS> 0u))
     
        do! PInvoke.CreateProcess(PCWSTR(), PWSTR(cmdlinePtr), NativePtr.nullPtr, NativePtr.nullPtr, false, flags, 
            NativePtr.toVoidPtr NativePtr.nullPtr<int>, PCWSTR(), si, pi) |> CheckResultBOOL "CreateProcess"

        let pi = NativePtr.read pi
        return (int32 pi.dwProcessId, pi.hProcess, pi.hThread)
    }

let openRunningProcess (pid : int32) =
    PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_SYNCHRONIZE, false, uint32 pid)
    |> CheckResultHandle "OpenProcess"

let resumeProcess (hThread : HANDLE) =
    if PInvoke.ResumeThread(hThread) = UInt32.MaxValue then
        Error (sprintf "[DebugActiveProcessStop] %s" (Win32Exception().Message))
    else Ok ()

let waitForProcessExit (hProcess : HANDLE) timeoutMs =
    match PInvoke.WaitForSingleObject(hProcess, timeoutMs) with
    | WAIT_EVENT.WAIT_OBJECT_0 -> Ok true
    | WAIT_EVENT.WAIT_TIMEOUT -> Ok false
    | WAIT_EVENT.WAIT_ABANDONED -> Error "[WaitForSingleObject] mutex abandoned"
    | WAIT_EVENT.WAIT_FAILED
    | _ -> Error (sprintf "[WaitForSingleObject] %s" (Win32Exception().Message))

