using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace LowLevelDesign.WinTrace.Handlers
{
    class RegistryTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;

        public RegistryTraceEventHandler(int pid, ITraceOutput traceOutput)
        {
            this.traceOutput = traceOutput;
            this.pid = pid;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
        }

        public void SubscribeToEvents(TraceEventParser parser)
        {
            var kernel = (KernelTraceEventParser)parser;
            kernel.RegistryClose += HandleRegistryTraceData;
            kernel.RegistryCreate += HandleRegistryTraceData;
            kernel.RegistryDelete += HandleRegistryTraceData;
            kernel.RegistryDeleteValue += HandleRegistryTraceData;
            kernel.RegistryEnumerateKey += HandleRegistryTraceData;
            kernel.RegistryEnumerateValueKey += HandleRegistryTraceData;
            kernel.RegistryFlush += HandleRegistryTraceData;
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
                string valueName = data.ValueName;
                string keyName = data.KeyName;

                string value;
                if (!string.IsNullOrEmpty(keyName) && !string.IsNullOrEmpty(valueName)) {
                    value = $"{keyName}\\{valueName}";
                } else if (!string.IsNullOrEmpty(keyName)) {
                    value = keyName;
                } else if (!string.IsNullOrEmpty(valueName)) {
                    value = valueName;
                } else {
                    value = null;
                }
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID,
                    data.EventName, value != null ? $"'{value}' (0x{keyHandle:X})" : $"(0x{keyHandle:X})");
            }
        }
    }
}
