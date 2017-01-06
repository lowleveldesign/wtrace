using LowLevelDesign.WinTrace.Tracing;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsRPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LowLevelDesign.WinTrace.Handlers
{
    sealed class RpcTraceEventHandler : ITraceEventHandler
    {
        private readonly TextWriter summaryOutput;
        private readonly TextWriter traceOutput;
        private readonly int pid;
        private readonly Dictionary<string, int> rpcSummary = new Dictionary<string, int>();
        private readonly Dictionary<Tuple<Guid, string, int>, string> awaitingClientCalls = new Dictionary<Tuple<Guid, string, int>, string>();
        private readonly Dictionary<Guid, string> rpcActivity = new Dictionary<Guid, string>();

        public RpcTraceEventHandler(int pid, TextWriter output, TraceOutputOptions options)
        {
            summaryOutput = options == TraceOutputOptions.NoSummary ? TextWriter.Null : output;
            traceOutput = options == TraceOutputOptions.OnlySummary ? TextWriter.Null : output;
            this.pid = pid;
        }

        public void PrintStatistics()
        {
            if (rpcSummary.Count == 0) {
                return;
            }
            summaryOutput.WriteLine("======= RPC =======");
            summaryOutput.WriteLine("Interface (Endpoint)  Number of interactions");
            foreach (var summary in rpcSummary.AsEnumerable().OrderByDescending(kv => kv.Value)) {
                summaryOutput.WriteLine($"{summary.Key} {summary.Value}");
            }
            summaryOutput.WriteLine();
        }

        public void SubscribeToEvents(TraceEventParser parser)
        {
            var rpcParser = (MicrosoftWindowsRPCTraceEventParser)parser;
            rpcParser.RpcClientCallStart += RpcClientCallStart;
            rpcParser.RpcClientCallStop += RpcClientCallStop;
            rpcParser.RpcServerCallStart += RpcServerCallStart;
            rpcParser.RpcServerCallStop += RpcServerCallStop;
        }

        private void RpcServerCallStop(RpcServerCallStopArgs data)
        {
            string rpcConnectionInfo;
            if (rpcActivity.TryGetValue(data.ActivityID, out rpcConnectionInfo)) {
                traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ProcessID}.{data.ThreadID}) {data.EventName}  " +
                    rpcConnectionInfo);
                rpcActivity.Remove(data.ActivityID);
            }
        }

        private void RpcServerCallStart(RpcServerCallStartArgs data)
        {
            if (data.ProcessID == pid) {
                var rpcConnectionInfo = $"--- {data.Protocol} --> {data.InterfaceUuid} ({data.Endpoint}) {data.ProcNum} {data.NetworkAddress}";
                rpcActivity.Add(data.ActivityID, rpcConnectionInfo);

                traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ProcessID}.{data.ThreadID}) {data.EventName} " +
                    rpcConnectionInfo);

                IncrementStatistics($"{data.InterfaceUuid} ({data.Endpoint})");
            } else if (data.Protocol == ProtocolSequences.LRPC ) {
                var key = new Tuple<Guid, string, int>(data.InterfaceUuid, data.Endpoint, data.ProcNum);
                string clientProcessInfo;
                if (awaitingClientCalls.TryGetValue(key, out clientProcessInfo)) {
                    var rpcConnectionInfo = $"<-- {data.Protocol} --- {data.InterfaceUuid} ({data.Endpoint}) {data.ProcNum} " +
                        $"{data.NetworkAddress} ({data.ProcessID}.{data.ThreadID})";
                    rpcActivity.Add(data.ActivityID, rpcConnectionInfo);

                    traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({clientProcessInfo}) {data.EventName} " +
                        rpcConnectionInfo);
                    awaitingClientCalls.Remove(key);
                }
            }
        }

        private void RpcClientCallStop(RpcClientCallStopArgs data)
        {
            string rpcConnectionInfo;
            if (rpcActivity.TryGetValue(data.ActivityID, out rpcConnectionInfo)) {
                traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ProcessID}.{data.ThreadID}) {data.EventName}  " +
                    rpcConnectionInfo);
                rpcActivity.Remove(data.ActivityID);
            }
        }

        private void RpcClientCallStart(RpcClientCallStartArgs data)
        {
            if (data.ProcessID == pid) {
                var rpcConnectionInfo = $"--- {data.Protocol} --> {data.InterfaceUuid} ({data.Endpoint}) {data.ProcNum} {data.NetworkAddress}";
                rpcActivity.Add(data.ActivityID, rpcConnectionInfo);

                traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ProcessID}.{data.ThreadID}) {data.EventName} " + 
                    rpcConnectionInfo);

                if (data.Protocol == ProtocolSequences.LRPC) {
                    awaitingClientCalls.Add(new Tuple<Guid, string, int>(
                        data.InterfaceUuid, data.Endpoint, data.ProcNum), $"{data.ProcessID}.{data.ThreadID}");
                }

                IncrementStatistics($"{data.InterfaceUuid} ({data.Endpoint})");
            }
        }

        private void IncrementStatistics(string summaryKey)
        {
            if (!rpcSummary.ContainsKey(summaryKey)) {
                rpcSummary.Add(summaryKey, 1);
            } else {
                rpcSummary[summaryKey]++;
            }
        }
    }
}
