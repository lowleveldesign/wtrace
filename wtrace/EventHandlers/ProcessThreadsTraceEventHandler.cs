using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Text;

namespace LowLevelDesign.WinTrace.EventHandlers
{
    sealed class ProcessThreadsTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;
        private readonly List<Tuple<int, string>> childProcesses = new List<Tuple<int, string>>();
        private readonly Action<int> actionToPerformWhenNewProcessIsCreated;

        public KernelTraceEventParser.Keywords RequiredKernelFlags => KernelTraceEventParser.Keywords.Process
            | KernelTraceEventParser.Keywords.Thread;

        public ProcessThreadsTraceEventHandler(int pid, ITraceOutput output, 
            Action<int> actionToPerformWhenNewProcessIsCreated)
        {
            traceOutput = output;
            this.pid = pid;
            this.actionToPerformWhenNewProcessIsCreated = actionToPerformWhenNewProcessIsCreated;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
            if (childProcesses.Count == 0) {
                return;
            }

            var buffer = new StringBuilder();
            foreach (var childProcess in childProcesses) { 
                if (buffer.Length != 0) {
                    buffer.AppendLine();
                }
                buffer.Append($"{childProcess.Item2} ({childProcess.Item1})");
            }
            traceOutput.WriteSummary($"Child processes ({pid})", buffer.ToString());
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
            }
        }

        private void HandleProcessStart(ProcessTraceData data)
        {
            if (data.ParentID == pid) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName,
                    $"{data.ProcessID} '{data.CommandLine}'");

                actionToPerformWhenNewProcessIsCreated(data.ProcessID);
                childProcesses.Add(new Tuple<int, string>(data.ProcessID, data.ProcessName));
            }
        }
    }
}
