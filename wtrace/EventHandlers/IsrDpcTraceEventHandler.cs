using LowLevelDesign.WinTrace.Utilities;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Diagnostics.Tracing.Session;
using System.Text;
using System;
using System.Reflection;
using Microsoft.Diagnostics.Tracing;

namespace LowLevelDesign.WinTrace.EventHandlers
{
    class IsrDpcTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly DriverImages loadedDrivers = new DriverImages();
        private readonly Dictionary<ulong, ExecutionStats> isrTimePerRoutine = new Dictionary<ulong, ExecutionStats>();
        private readonly Dictionary<ulong, ExecutionStats> dpcTimePerRoutine = new Dictionary<ulong, ExecutionStats>();

        private readonly byte[] bufferForInitialTimeValue = new byte[sizeof(Int64)];
        private Func<long, double> QPCTimeToRelMSec;

        public IsrDpcTraceEventHandler(ITraceOutput traceOutput)
        {
            this.traceOutput = traceOutput;
        }

        public KernelTraceEventParser.Keywords RequiredKernelFlags => KernelTraceEventParser.Keywords.Interrupt
            | KernelTraceEventParser.Keywords.DeferedProcedureCalls | KernelTraceEventParser.Keywords.ImageLoad;

        public void SubscribeToSession(TraceEventSession session)
        {
            var kernel = session.Source.Kernel;
            kernel.PerfInfoISR += HandleIsr;
            kernel.ImageDCStart += HandleImageLoad;
            kernel.ImageLoad += HandleImageLoad;
            kernel.PerfInfoDPC += HandleDpc;
            kernel.PerfInfoThreadedDPC += HandleDpc;
            kernel.PerfInfoTimerDPC += HandleDpc;

            // stub method for valid calculation of the elapsed time for ISR/DPC events
            var methodInfo = session.Source.GetType().GetMethod("QPCTimeToRelMSec", BindingFlags.Instance | BindingFlags.NonPublic);
            var traceSource = session.Source;
            var args = new object[1];
            QPCTimeToRelMSec = (long qpcTime) => {
                args[0] = qpcTime;
                return (double)methodInfo.Invoke(traceSource, args);
            };
        }

        private double ComputeElapsedTimeMSec(TraceEvent ev)
        {
            ev.EventData(bufferForInitialTimeValue, 0, 0, bufferForInitialTimeValue.Length);
            var initialTime = BitConverter.ToInt64(bufferForInitialTimeValue, 0);
            return ev.TimeStampRelativeMSec - QPCTimeToRelMSec(initialTime);
        }

        private void HandleIsr(ISRTraceData data)
        {
            UpdateExecutionStats(isrTimePerRoutine, data.Routine, ComputeElapsedTimeMSec(data));
        }

        private void HandleDpc(DPCTraceData data)
        {
            UpdateExecutionStats(dpcTimePerRoutine, data.Routine, ComputeElapsedTimeMSec(data));
        }

        private static void UpdateExecutionStats(Dictionary<ulong, ExecutionStats> historicStats, ulong routine, double elapsedTimeMSec)
        {
            ExecutionStats stats;
            if (!historicStats.TryGetValue(routine, out stats)) {
                historicStats.Add(routine, new ExecutionStats {
                    Count = 1,
                    ElapsedTimeMSec = elapsedTimeMSec
                });
            } else {
                stats.Count += 1;
                stats.ElapsedTimeMSec += elapsedTimeMSec;
            }
        }

        private void HandleImageLoad(ImageLoadTraceData data)
        {
            if (data.ProcessID == 0) {
                // System process (contain driver dlls)
                loadedDrivers.AddImage(new FileImageInMemory(data.FileName, data.ImageBase,
                    data.ImageSize));
                return;
            }
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
            if (isrTimePerRoutine.Count > 0) {
                PrintExecutionStatistics("ISR", isrTimePerRoutine);
            }
            if (dpcTimePerRoutine.Count > 0) {
                PrintExecutionStatistics("DPC", dpcTimePerRoutine);
            }
        }

        private void PrintExecutionStatistics(string title, Dictionary<ulong, ExecutionStats> statsPerRoutine)
        {
            var statsPerDriver = new Dictionary<string, ExecutionStats>();
            foreach (var kv in statsPerRoutine) {
                // resolve the routing address
                var driverImage = loadedDrivers.FindImage(kv.Key);
                Debug.Assert(driverImage != null);
                if (driverImage != null) {
                    ExecutionStats driverStats;
                    if (!statsPerDriver.TryGetValue(driverImage.FileName, out driverStats)) {
                        statsPerDriver.Add(driverImage.FileName, kv.Value);
                    } else {
                        driverStats.Count += kv.Value.Count;
                        driverStats.ElapsedTimeMSec += kv.Value.ElapsedTimeMSec;
                    }
                }
            }

            var buffer = new StringBuilder();
            // sort the stats by timespan
            foreach (var kv in statsPerDriver.OrderByDescending(kv => kv.Value.ElapsedTimeMSec)) {
                if (buffer.Length != 0) {
                    buffer.AppendLine();
                }
                buffer.Append($"'{kv.Key}', total: {kv.Value.ElapsedTimeMSec:#,0.000}ms ({kv.Value.Count} event(s))");
            }
            traceOutput.WriteSummary(title, buffer.ToString());

        }
    }
}
