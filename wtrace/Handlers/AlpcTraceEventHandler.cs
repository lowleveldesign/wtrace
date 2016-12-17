using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;
using System.IO;

namespace LowLevelDesign.WinTrace.Handlers
{
    class AlpcTraceEventHandler : ITraceEventHandler
    {
        private readonly TextWriter summaryOutput;
        private readonly TextWriter traceOutput;
        private readonly int pid;
        private readonly Dictionary<int, Tuple<int, string>> sentMessages = new Dictionary<int, Tuple<int, string>>();
        private readonly HashSet<string> connectedProcesses = new HashSet<string>();

        public AlpcTraceEventHandler(int pid, TextWriter output, TraceOutputOptions options)
        {
            summaryOutput = options == TraceOutputOptions.NoSummary ? TextWriter.Null : output;
            traceOutput = options == TraceOutputOptions.OnlySummary ? TextWriter.Null : output;
            this.pid = pid;
        }

        public void SubscribeToEvents(KernelTraceEventParser kernel)
        {
            kernel.ALPCReceiveMessage += HandleALPCReceiveMessage;
            kernel.ALPCSendMessage += HandleALPCSendMessage;
            //kernel.ALPCUnwait += HandleALPCUnwait;
            //kernel.ALPCWaitForNewMessage += HandleALPCWaitForNewMessage;
            kernel.ALPCWaitForReply += HandleALPCWaitForReply;
        }

        private void HandleALPCWaitForReply(ALPCWaitForReplyTraceData data)
        {
            UpdateCache(data.ProcessID, data.ProcessName, data.MessageID);

            if (pid == data.ProcessID) {
                traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} (0x{data.MessageID:X})");
            }
        }

        private void HandleALPCSendMessage(ALPCSendMessageTraceData data)
        {
            UpdateCache(data.ProcessID, data.ProcessName, data.MessageID);
        }

        private void HandleALPCReceiveMessage(ALPCReceiveMessageTraceData data)
        {
            Tuple<int, string> senderProcess;
            if (sentMessages.TryGetValue(data.MessageID, out senderProcess)) {
                if (data.ProcessID == pid) {
                    connectedProcesses.Add($"{senderProcess.Item2} ({senderProcess.Item1})");
                    traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ALPC {data.ProcessName} ({data.ProcessID}) " +
                        $"<--(0x{data.MessageID:X})--- {senderProcess.Item2} ({senderProcess.Item1})");
                } else if (senderProcess.Item1 == pid) {
                    connectedProcesses.Add($"{data.ProcessName} ({data.ProcessID})");
                    traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ALPC {senderProcess.Item2} ({senderProcess.Item1}) " +
                        $"---(0x{data.MessageID:X})--> {data.ProcessName} ({data.ProcessID})");
                }
            }
        }

        private void UpdateCache(int processId, string processName, int messageId)
        {
            if (sentMessages.ContainsKey(messageId)) {
                sentMessages[messageId] = new Tuple<int, string>(processId, processName);
            } else {
                sentMessages.Add(messageId, new Tuple<int, string>(processId, processName));
            }
        }

        public void PrintStatistics()
        {
            if (connectedProcesses.Count == 0) {
                return;
            }
            summaryOutput.WriteLine("======= ALPC =======");
            summaryOutput.WriteLine("Filtered process connected through ALPC with:");
            foreach (var process in connectedProcesses) {
                summaryOutput.WriteLine($"- {process}");
            }
            summaryOutput.WriteLine();
        }
    }
}
