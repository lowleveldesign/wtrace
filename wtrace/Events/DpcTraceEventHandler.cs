using LowLevelDesign.WinTrace.Utilities;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace LowLevelDesign.WinTrace.Handlers
{
    class DpcTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;

        public DpcTraceEventHandler(int pid, ITraceOutput traceOutput)
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
            kernel.PerfInfoDPC += HandleDpc;
            kernel.PerfInfoThreadedDPC += HandleThreadedDpc;
            kernel.PerfInfoTimerDPC += HandleTimerDpc;
        }

        private void HandleTimerDpc(DPCTraceData obj)
        {
            throw new System.NotImplementedException();
        }

        private void HandleThreadedDpc(DPCTraceData obj)
        {
            throw new System.NotImplementedException();
        }

        private void HandleDpc(DPCTraceData obj)
        {
            throw new System.NotImplementedException();
        }
    }
}
