using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LowLevelDesign.WinTrace.Handlers
{
    class AlpcTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;
        private readonly Dictionary<int, Tuple<int, string, int>> sentMessages = new Dictionary<int, Tuple<int, string, int>>();
        private readonly HashSet<string> connectedProcesses = new HashSet<string>();

        private TraceEventSource traceEventSource;

        public AlpcTraceEventHandler(int pid, ITraceOutput output)
        {
            traceOutput = output;
            this.pid = pid;
        }

        public void SubscribeToEvents(TraceEventParser parser)
        {
            var kernel = (KernelTraceEventParser)parser;
            kernel.ALPCReceiveMessage += HandleALPCReceiveMessage;
            kernel.ALPCSendMessage += HandleALPCSendMessage;
            //kernel.ALPCUnwait += HandleALPCUnwait;
            //kernel.ALPCWaitForNewMessage += HandleALPCWaitForNewMessage;
            kernel.ALPCWaitForReply += HandleALPCWaitForReply;

            traceEventSource = parser.Source;
        }

        private void HandleALPCWaitForReply(ALPCWaitForReplyTraceData data)
        {
            UpdateCache(data.ProcessID, data.ProcessName, data.ThreadID, data.MessageID);

            if (pid == data.ProcessID) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, $"(0x{data.MessageID:X})");
            }
        }

        private void HandleALPCSendMessage(ALPCSendMessageTraceData data)
        {
            UpdateCache(data.ProcessID, data.ProcessName, data.ThreadID, data.MessageID);
        }

        private void HandleALPCReceiveMessage(ALPCReceiveMessageTraceData data)
        {
            Tuple<int, string, int> senderProcess;
            if (sentMessages.TryGetValue(data.MessageID, out senderProcess)) {
                if (data.ProcessID == pid) {
                    connectedProcesses.Add($"{senderProcess.Item2} ({senderProcess.Item1})");
                    traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, "ALPC", 
                        $"{data.ProcessName} <--(0x{data.MessageID:X})--- {senderProcess.Item2} ({senderProcess.Item1}.{senderProcess.Item3})");
                } else if (senderProcess.Item1 == pid) {
                    connectedProcesses.Add($"{data.ProcessName} ({data.ProcessID})");
                    traceOutput.Write(data.TimeStampRelativeMSec, senderProcess.Item1, senderProcess.Item3, "ALPC", 
                        $"{senderProcess.Item2} ---(0x{data.MessageID:X})--> {data.ProcessName} ({data.ProcessID}.{data.ThreadID})");
                }
            }
        }

        private void UpdateCache(int processId, string processName, int threadId, int messageId)
        {
            if (sentMessages.ContainsKey(messageId)) {
                sentMessages[messageId] = new Tuple<int, string, int>(processId, processName, threadId);
            } else {
                sentMessages.Add(messageId, new Tuple<int, string, int>(processId, processName, threadId));
            }
        }

        public void PrintStatistics()
        {
            if (connectedProcesses.Count == 0) {
                return;
            }
            Debug.Assert(traceEventSource != null);
            foreach (var process in connectedProcesses) {
                traceOutput.Write(traceEventSource.SessionEndTimeRelativeMSec, pid, 0, "Summary/ALPC", $"endpoint: {process}");
            }
        }
    }
}
