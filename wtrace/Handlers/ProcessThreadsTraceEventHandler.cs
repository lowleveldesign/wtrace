using LowLevelDesign.WinTrace.Tracing;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.IO;

namespace LowLevelDesign.WinTrace.Handlers
{
    sealed class ProcessThreadsTraceEventHandler : ITraceEventHandler
    {
        private readonly TextWriter summaryOutput;
        private readonly TextWriter traceOutput;
        private readonly int pid;
        private int noOfChildProcessesStarted = 0;
        private int noOfThreadStarted = 0;

        public ProcessThreadsTraceEventHandler(int pid, TextWriter output, TraceOutputOptions options)
        {
            summaryOutput = options == TraceOutputOptions.NoSummary ? TextWriter.Null : output;
            traceOutput = options == TraceOutputOptions.OnlySummary ? TextWriter.Null : output;
            this.pid = pid;
        }

        public void PrintStatistics()
        {
            summaryOutput.WriteLine("======= Process/Thread =======");
            summaryOutput.WriteLine($"Number of child processes started: {noOfChildProcessesStarted}");
            summaryOutput.WriteLine($"Number of threads started: {noOfThreadStarted}");
            summaryOutput.WriteLine();
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
                traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ProcessID}.{data.ThreadID}) {data.EventName} " + 
                    $"{data.ParentProcessID} ({data.ParentThreadID})");
                noOfThreadStarted++;
            }
        }

        private void HandleProcessStart(ProcessTraceData data)
        {
            if (data.ParentID == pid) {
                traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ProcessID}.{data.ThreadID}) {data.EventName} " + 
                    $"{data.ProcessID} '{data.CommandLine}'");
                noOfChildProcessesStarted++;
            }
        }
    }
}
