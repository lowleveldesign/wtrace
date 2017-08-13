using System;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;

namespace LowLevelDesign.WinTrace.EventHandlers
{
    sealed class ProcessThreadsTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;
        private int noOfChildProcessesStarted = 0;
        private int noOfThreadStarted = 0;

        public KernelTraceEventParser.Keywords RequiredKernelFlags => KernelTraceEventParser.Keywords.Process
            | KernelTraceEventParser.Keywords.Thread;

        public ProcessThreadsTraceEventHandler(int pid, ITraceOutput output)
        {
            traceOutput = output;
            this.pid = pid;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
        }

        public void SubscribeToSession(TraceEventSession session)
        {
            var kernel = session.Source.Kernel;
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
