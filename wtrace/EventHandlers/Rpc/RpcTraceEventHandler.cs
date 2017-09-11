using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsRPC;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LowLevelDesign.WinTrace.EventHandlers.Rpc
{
    sealed class RpcTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;
        private readonly Dictionary<string, int> rpcSummary = new Dictionary<string, int>();
        private readonly Dictionary<Tuple<Guid, string, int>, Tuple<int, int>> awaitingClientCalls = new Dictionary<Tuple<Guid, string, int>, Tuple<int, int>>();
        private readonly Dictionary<Guid, string> rpcActivity = new Dictionary<Guid, string>();

        public KernelTraceEventParser.Keywords RequiredKernelFlags => KernelTraceEventParser.Keywords.None;

        public RpcTraceEventHandler(int pid, ITraceOutput output)
        {
            traceOutput = output;
            this.pid = pid;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
            if (rpcSummary.Count == 0) {
                return;
            }
            var buffer = new StringBuilder();
            foreach (var summary in rpcSummary.AsEnumerable().OrderByDescending(kv => kv.Value)) {
                if (buffer.Length != 0) {
                    buffer.AppendLine();
                }
                buffer.Append($"endpoint: {summary.Key}, connections: {summary.Value}");
            }
            traceOutput.WriteSummary($"RPC ({pid})", buffer.ToString());
        }

        public void SubscribeToSession(TraceEventSession session)
        {
            var rpcParser = new MicrosoftWindowsRPCTraceEventParser(session.Source);

            rpcParser.RpcClientCallStart += RpcClientCallStart;
            rpcParser.RpcClientCallStop += RpcClientCallStop;
            rpcParser.RpcServerCallStart += RpcServerCallStart;
            rpcParser.RpcServerCallStop += RpcServerCallStop;

            session.EnableProvider(MicrosoftWindowsRPCTraceEventParser.ProviderGuid, TraceEventLevel.Informational);
        }

        private void RpcServerCallStop(RpcServerCallStopArgs data)
        {
            string rpcConnectionInfo;
            if (rpcActivity.TryGetValue(data.ActivityID, out rpcConnectionInfo)) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, rpcConnectionInfo);
                rpcActivity.Remove(data.ActivityID);
            }
        }

        private void RpcServerCallStart(RpcServerCallStartArgs data)
        {
            if (data.ProcessID == pid) {
                var rpcConnectionInfo = $"--- {data.Protocol} --> {data.InterfaceUuid} ({data.Endpoint}) {data.ProcNum} {data.NetworkAddress}";
                rpcActivity.Add(data.ActivityID, rpcConnectionInfo);

                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, rpcConnectionInfo);

                IncrementStatistics($"{data.InterfaceUuid} ({data.Endpoint})");
            } else if (data.Protocol == ProtocolSequences.LRPC ) {
                var key = new Tuple<Guid, string, int>(data.InterfaceUuid, data.Endpoint, data.ProcNum);
                Tuple<int, int> clientProcessInfo;
                if (awaitingClientCalls.TryGetValue(key, out clientProcessInfo)) {
                    var rpcConnectionInfo = $"<-- {data.Protocol} --- {data.InterfaceUuid} ({data.Endpoint}) {data.ProcNum} " +
                        $"{data.NetworkAddress} ({data.ProcessID}.{data.ThreadID})";
                    rpcActivity.Add(data.ActivityID, rpcConnectionInfo);

                    traceOutput.Write(data.TimeStampRelativeMSec, clientProcessInfo.Item1, clientProcessInfo.Item2, data.EventName,
                        rpcConnectionInfo);
                    awaitingClientCalls.Remove(key);
                }
            }
        }

        private void RpcClientCallStop(RpcClientCallStopArgs data)
        {
            string rpcConnectionInfo;
            if (rpcActivity.TryGetValue(data.ActivityID, out rpcConnectionInfo)) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, rpcConnectionInfo);
                rpcActivity.Remove(data.ActivityID);
            }
        }

        private void RpcClientCallStart(RpcClientCallStartArgs data)
        {
            if (data.ProcessID == pid) {
                var rpcConnectionInfo = $"--- {data.Protocol} --> {data.InterfaceUuid} ({data.Endpoint}) {data.ProcNum} {data.NetworkAddress}";
                rpcActivity.Add(data.ActivityID, rpcConnectionInfo);

                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, rpcConnectionInfo);

                if (data.Protocol == ProtocolSequences.LRPC) {
                    var key = new Tuple<Guid, string, int>(data.InterfaceUuid, data.Endpoint, data.ProcNum);
                    if (!awaitingClientCalls.ContainsKey(key)) {
                        awaitingClientCalls.Add(key, new Tuple<int, int>(data.ProcessID, data.ThreadID));
                    }
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
