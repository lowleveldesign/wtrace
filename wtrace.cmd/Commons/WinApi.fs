module LowLevelDesign.WTrace.WinApi

open System
open PInvoke

type SHandle = Kernel32.SafeObjectHandle

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

let CheckResultBool b = 
    if b then Ok () else Error (Win32Exception().Message)

let CheckResultHandle h =
    if h = Kernel32.INVALID_HANDLE_VALUE then Error (Win32Exception().Message)
    else Ok h

let CheckResultSafeHandle (h : SHandle) =
    if h.IsInvalid then Error (Win32Exception().Message)
    else Ok h

let Win32ErrorMessage (err : int) = 
    Win32Exception(err).Message


let startProcessSuspended (args : seq<string>) spawnNewConsole =
    result {
        let mutable pi = Kernel32.PROCESS_INFORMATION()
        let mutable si = Kernel32.STARTUPINFO(hStdInput = IntPtr.Zero,
                                              hStdOutput = IntPtr.Zero,
                                              hStdError = IntPtr.Zero)


        let flags = 
            if spawnNewConsole then
                Kernel32.CreateProcessFlags.CREATE_NEW_CONSOLE
            else Kernel32.CreateProcessFlags.None

        let flags = flags |||
                    Kernel32.CreateProcessFlags.CREATE_SUSPENDED |||
                    Kernel32.CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT

        do! Kernel32.CreateProcess(null, String.Join(" ", args), IntPtr.Zero, IntPtr.Zero, false,
                flags, IntPtr.Zero, null, &si, &pi) |> CheckResultBool

        return (pi.dwProcessId, new SHandle(pi.hProcess), new SHandle(pi.hThread))
    }

let openRunningProcess pid =
    Kernel32.OpenProcess(Kernel32.ACCESS_MASK(uint32 Kernel32.ACCESS_MASK.StandardRight.SYNCHRONIZE), false, pid)
    |> CheckResultSafeHandle

let resumeThread hThread =
    if Kernel32.ResumeThread(hThread) = -1 then
        Error (Win32Exception().Message)
    else Ok ()

let waitForProcessExit hProcess timeoutMs =
    match Kernel32.WaitForSingleObject(hProcess, timeoutMs) with
    | Kernel32.WaitForSingleObjectResult.WAIT_OBJECT_0 -> Ok true
    | Kernel32.WaitForSingleObjectResult.WAIT_TIMEOUT -> Ok false
    | Kernel32.WaitForSingleObjectResult.WAIT_ABANDONED -> Error "waitForProcessExit: mutex abandoned"
    | Kernel32.WaitForSingleObjectResult.WAIT_FAILED
    | _ -> Error (Win32Exception().Message)

