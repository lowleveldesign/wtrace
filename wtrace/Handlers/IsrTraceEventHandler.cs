using LowLevelDesign.WinTrace.Utilities;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LowLevelDesign.WinTrace.Handlers
{
    class IsrTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;
        private readonly Dictionary<ulong, ExecutionStats> isrTimePerRoutine = new Dictionary<ulong, ExecutionStats>();

        public IsrTraceEventHandler(int pid, ITraceOutput traceOutput)
        {
            this.traceOutput = traceOutput;
            this.pid = pid;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
            // sort the stats by timespan
            foreach (var kv in isrTimePerRoutine.OrderByDescending(kv => kv.Value.TotalTime)) {
                // resolve the routing address
                var driverImage = DriverImageUtilities.FindImage(kv.Key);
                Debug.Assert(driverImage != null);
                if (driverImage != null) {
                    traceOutput.Write(sessionEndTimeInMs, 0, 0, 
                        "Summary/ISR", $"'{driverImage.FileName}' {kv.Value.Count} {kv.Value.TotalTime.TotalMilliseconds}ms");
                }
            }
        }

        public void SubscribeToEvents(TraceEventParser parser)
        {
            var kernel = (KernelTraceEventParser)parser;
            kernel.PerfInfoISR += HandleIsr;
        }

        private void HandleIsr(ISRTraceData data)
        {
            var delta = data.TimeStamp.Subtract(data.InitialTime);
            ExecutionStats stats;
            if (!isrTimePerRoutine.TryGetValue(data.Routine, out stats)) {
                isrTimePerRoutine.Add(data.Routine, new ExecutionStats {
                    Count = 1,
                    TotalTime = delta
                });
            } else {
                stats.Count += 1;
                stats.TotalTime += delta;
            }
        }
    }
}
