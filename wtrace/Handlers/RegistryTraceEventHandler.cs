﻿using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.Collections.Generic;
using System.Linq;

namespace LowLevelDesign.WinTrace.Handlers
{
    class RegistryTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;
        private readonly Dictionary<string, int> registrySummary = new Dictionary<string, int>();

        public RegistryTraceEventHandler(int pid, ITraceOutput traceOutput)
        {
            this.traceOutput = traceOutput;
            this.pid = pid;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
            // Currently turned off - number of events is overwhelming
            if (registrySummary.Count == 0) {
                return;
            }
            foreach (var summary in registrySummary.OrderByDescending(kv => kv.Value)) {
                traceOutput.Write(sessionEndTimeInMs, pid, 0,
                    "Summary/Registry", $"'{summary.Key}' Opened: {summary.Value:#,0}");
            }
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

                // we count RegistryOpen event for statistics
                if ((byte)data.Opcode == 11 && !string.IsNullOrEmpty(keyName)) {
                    int openCount;
                    if (registrySummary.TryGetValue(keyName, out openCount)) {
                        registrySummary[keyName] = openCount + 1;
                    } else {
                        registrySummary.Add(keyName, 1);
                    }
                }

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
