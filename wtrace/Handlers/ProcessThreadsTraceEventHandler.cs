using LowLevelDesign.WinTrace.Tracing;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.Diagnostics;
using System.IO;

namespace LowLevelDesign.WinTrace.Handlers
{
    sealed class ProcessThreadsTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;
        private int noOfChildProcessesStarted = 0;
        private int noOfThreadStarted = 0;

        public ProcessThreadsTraceEventHandler(int pid, ITraceOutput output)
        {
            traceOutput = output;
            this.pid = pid;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
            traceOutput.Write(sessionEndTimeInMs, pid, 0, "Summary/Process", 
                $"Number of child processes started: {noOfChildProcessesStarted}");
            traceOutput.Write(sessionEndTimeInMs, pid, 0, "Summary/Thread", 
                $"Number of threads started: {noOfThreadStarted}");
        }

        public void SubscribeToEvents(TraceEventParser parser)
        {
            var kernel = (KernelTraceEventParser)parser;
            kernel.ProcessStart += HandleProcessStart;
            kernel.ThreadStart += HandleThreadStart;
        }

        private void HandleThreadStart(ThreadTraceData data)
        {
            if (data.ProcessID == pid) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName,
                    $"{data.ParentProcessID} ({data.ParentThreadID})");
                noOfThreadStarted++;
            }
        }

        private void HandleProcessStart(ProcessTraceData data)
        {
            if (data.ParentID == pid) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName,
                    $"{data.ProcessID} '{data.CommandLine}'");
                noOfChildProcessesStarted++;
            }
        }
    }
}
