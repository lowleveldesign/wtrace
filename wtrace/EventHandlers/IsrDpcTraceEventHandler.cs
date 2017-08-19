using LowLevelDesign.WinTrace.Utilities;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Diagnostics.Tracing.Session;
using System.Text;

namespace LowLevelDesign.WinTrace.EventHandlers
{
    class IsrDpcTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;
        private readonly DriverImages loadedDrivers = new DriverImages();
        private readonly Dictionary<ulong, ExecutionStats> isrTimePerRoutine = new Dictionary<ulong, ExecutionStats>();
        private readonly Dictionary<ulong, ExecutionStats> dpcTimePerRoutine = new Dictionary<ulong, ExecutionStats>();

        public IsrDpcTraceEventHandler(int pid, ITraceOutput traceOutput)
        {
            this.traceOutput = traceOutput;
            this.pid = pid;
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
        }

        private void HandleIsr(ISRTraceData data)
        {
            //FIXME: UpdateExecutionStats(isrTimePerRoutine, data.Routine, data.ElapsedTimeMSec);
        }

        private void HandleDpc(DPCTraceData data)
        {
            // FIXME: UpdateExecutionStats(dpcTimePerRoutine, data.Routine, data.ElapsedTimeMSec);
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
            PrintExecutionStatistics("ISR", isrTimePerRoutine);
            PrintExecutionStatistics("DPC", dpcTimePerRoutine);
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
