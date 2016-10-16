using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.Collections.Generic;
using System.IO;
using System;

namespace LowLevelDesign.WinTrace.Handlers
{
    class RegistryTraceEventHandler : ITraceEventHandler
    {
        private readonly TextWriter output;
        private readonly int pid;
        private readonly Dictionary<ulong, string> registryHandleToKeyNameMap = new Dictionary<ulong, string>();

        public RegistryTraceEventHandler(int pid, TextWriter output)
        {
            this.output = output;
            this.pid = pid;

        }

        public void PrintStatistics()
        {
            output.WriteLine("======= Registry =======");
            output.WriteLine("TBD");
        }

        public void SubscribeToEvents(KernelTraceEventParser kernel)
        {
            kernel.RegistryClose += HandleRegistryTraceData;
            kernel.RegistryCreate += HandleRegistryTraceData;
            kernel.RegistryDelete += HandleRegistryTraceData;
            kernel.RegistryDeleteValue += HandleRegistryTraceData;
            kernel.RegistryEnumerateKey += HandleRegistryTraceData;
            kernel.RegistryEnumerateValueKey += HandleRegistryTraceData;
            kernel.RegistryFlush += HandleRegistryTraceData;
            kernel.RegistryKCBCreate += HandleRegistryTraceData;
            kernel.RegistryKCBDelete += HandleRegistryTraceData;
            kernel.RegistryKCBRundownBegin += HandleRegistryTraceData;
            kernel.RegistryKCBRundownEnd += HandleRegistryTraceData;
            kernel.RegistryOpen += HandleRegistryTraceData;
            kernel.RegistryQuery += HandleRegistryTraceData;
            kernel.RegistryQueryMultipleValue += HandleRegistryTraceData;
            kernel.RegistryQueryValue += HandleRegistryTraceData;
            kernel.RegistrySetInformation += HandleRegistryTraceData;
            kernel.RegistrySetValue += HandleRegistryTraceData;
            kernel.RegistryVirtualize += HandleRegistryTraceData;
        }

        private void HandleRegistryTraceData(RegistryTraceData data)
        {
            if (data.ProcessID == pid) {
                ulong keyHandle = data.KeyHandle;
                string keyName;
                if (!registryHandleToKeyNameMap.TryGetValue(keyHandle, out keyName)) {
                    keyName = data.KeyName;
                    if (keyName != null) {
                        registryHandleToKeyNameMap.Add(keyHandle, keyName);
                    }
                } 
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} " + 
                    $"'{keyName}' '{data.ValueName}' (0x{keyHandle:X})");
            }
        }
    }
}
