module LowLevelDesign.WTrace.WinApi

open System
open PInvoke


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

let Win32ErrorMessage (err : int) = 
    Win32Exception(err).Message

