module LowLevelDesign.WTrace.Events.Registry

open System
open System.Collections.Generic
open System.IO
open System.Security.Principal
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events.HandlerCommons

type private RegistryHandlerState = {
    Broadcast : EventBroadcast
    // a state to keep a map of key handles (KCB) to actual key names
    KeyHandleToName : Dictionary<uint64, string>
}

[<AutoOpen>]
module private H =
    let currentUserSid = WindowsIdentity.GetCurrent().User.ToString();

    let knownRegistryNames = [|
        (sprintf "\\Registry\\User\\%s_classes" currentUserSid, "HKCR")
        (sprintf "\\Registry\\User\\%s" currentUserSid, "HKCU")
        ("\\Registry\\User", "HKU")
        ("\\Registry\\Machine", "HKLM")
    |]

    let abbreviate (keyName : string) =
        let abbr = knownRegistryNames |> Array.tryFind (fun (n, _) -> keyName.StartsWith(n, StringComparison.OrdinalIgnoreCase))
        match abbr with
        | Some (n, a) -> a + (keyName.Substring(n.Length))
        | None -> keyName

    let handleKCBCreateEvent state (ev : RegistryTraceData) =
        state.KeyHandleToName.[ev.KeyHandle] <- abbreviate ev.KeyName

    let handleKCBDeleteEvent state (ev : RegistryTraceData) =
        state.KeyHandleToName.Remove(ev.KeyHandle) |> ignore

    let handleRegistryEvent id state (ev : RegistryTraceData) =
        let path =
            if ev.KeyHandle = 0UL then
                abbreviate ev.KeyName
            else
                let baseKeyName = 
                    match state.KeyHandleToName.TryGetValue(ev.KeyHandle) with
                    | (true, name) -> name
                    | (false, _) -> sprintf "<0x%X>" ev.KeyHandle
                Path.Combine(baseKeyName, ev.KeyName)

        let ev = toEvent ev id "" path "" ev.Status
        state.Broadcast.publishTraceEvent (TraceEventWithFields (ev, noFields))

    let subscribe (source : TraceEventSource, isRundown, idgen, state : obj) =
        let state = state :?> RegistryHandlerState
        let handleEvent h = Action<_>(handleEvent idgen state h)
        let handle h = Action<_>(h state)
        if isRundown then
            source.Kernel.add_RegistryKCBRundownBegin(handle handleKCBCreateEvent)
            source.Kernel.add_RegistryKCBRundownEnd(handle handleKCBCreateEvent)
        else
            source.Kernel.add_RegistryKCBCreate(handle handleKCBCreateEvent)
            source.Kernel.add_RegistryKCBDelete(handle handleKCBDeleteEvent)

            source.Kernel.add_RegistryCreate(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistryOpen(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistryClose(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistryFlush(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistryEnumerateKey(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistryQuery(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistrySetInformation(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistryVirtualize(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistryDelete(handleEvent handleRegistryEvent)
            
            source.Kernel.add_RegistryEnumerateValueKey(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistryQueryValue(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistryQueryMultipleValue(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistrySetValue(handleEvent handleRegistryEvent)
            source.Kernel.add_RegistryDeleteValue(handleEvent handleRegistryEvent)


let createEtwHandler () =
    {
        KernelFlags = NtKeywords.Registry
        KernelStackFlags = NtKeywords.Registry
        KernelRundownFlags = NtKeywords.Registry
        Providers = Array.empty<EtwEventProvider>
        Initialize = 
            fun (broadcast) -> ({
                Broadcast = broadcast
                KeyHandleToName = Dictionary<uint64, string>()
            } :> obj)
        Subscribe = subscribe
    }

