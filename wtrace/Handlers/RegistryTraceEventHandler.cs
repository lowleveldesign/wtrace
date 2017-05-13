using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.Collections.Generic;

namespace LowLevelDesign.WinTrace.Handlers
{
    class RegistryTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput output;
        private readonly int pid;
        private readonly Dictionary<ulong, string> registryHandleToKeyNameMap = new Dictionary<ulong, string>();

        public RegistryTraceEventHandler(int pid, ITraceOutput output)
        {
            this.output = output;
            this.pid = pid;

        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
            // FIXME: to implement
        }

        public void SubscribeToEvents(TraceEventParser parser)
        {
            var kernel = (KernelTraceEventParser)parser;
            kernel.RegistryClose += HandleRegistryCloseTraceData;
            kernel.RegistryCreate += HandleRegistryTraceData;
            kernel.RegistryDelete += HandleRegistryCloseTraceData;
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

        private void HandleRegistryCloseTraceData(RegistryTraceData data)
        {
            if (data.ProcessID == pid) {
                ulong keyHandle = data.KeyHandle;
                string keyName;
                if (!registryHandleToKeyNameMap.TryGetValue(keyHandle, out keyName)) {
                    registryHandleToKeyNameMap.Remove(keyHandle);
                }
                output.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID,
                    data.EventName, $"'{keyName}' (0x{keyHandle:X})");
            }
        }

        private void HandleRegistryTraceData(RegistryTraceData data)
        {
            if (data.ProcessID == pid) {
                ulong keyHandle = data.KeyHandle;
                string valueName = data.ValueName;
                string keyName;
                if (keyHandle != 0) {
                    if (!registryHandleToKeyNameMap.TryGetValue(keyHandle, out keyName)) {
                        keyName = data.KeyName;
                        if (keyName != null) {
                            registryHandleToKeyNameMap.Add(keyHandle, keyName);
                        }
                    }
                } else {
                    keyName = data.KeyName;
                }
                string value;
                if (keyName != null && valueName != null) {
                    value = $"{keyName}\\{valueName}";
                } else if (keyName != null) {
                    value = keyName;
                } else {
                    value = valueName;
                }
                output.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID,
                    data.EventName, $"'{data.KeyName}{data.ValueName}' (0x{keyHandle:X})");
            }
        }
    }
}
