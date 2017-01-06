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

        public RpcTraceEventHandler(int pid, TextWriter output, TraceOutputOptions options)
        {
            summaryOutput = options == TraceOutputOptions.NoSummary ? TextWriter.Null : output;
            traceOutput = options == TraceOutputOptions.OnlySummary ? TextWriter.Null : output;
            this.pid = pid;
        }

        public void PrintStatistics()
        {
            //if (networkIoSummary.Count == 0) {
            //    return;
            //}
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

        private void RpcServerCallStop(RpcServerCallStopArgs ev)
        {
        }

        private void RpcServerCallStart(RpcServerCallStartArgs data)
        {
            //traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({pid}) {data.EventName} " + 
            //    $"<-- {data.Protocol} --- ({data.ProcessID}.{data.ThreadID}) {data.InterfaceUuid} " + 
            //    $"({data.Endpoint}) {data.ProcNum} {data.NetworkAddress}");
        }

        private void RpcClientCallStop(RpcClientCallStopArgs ev)
        {
        }

        private void RpcClientCallStart(RpcClientCallStartArgs data)
        {
            if (data.ProcessID == pid) {
                traceOutput.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ProcessID}.{data.ThreadID}) {data.EventName} " + 
                    $"--- {data.Protocol} --> {data.InterfaceUuid} ({data.Endpoint}) {data.ProcNum} {data.NetworkAddress}");

                string summaryKey = $"{data.InterfaceUuid} ({data.Endpoint})";
                if (!rpcSummary.ContainsKey(summaryKey)) {
                    rpcSummary.Add(summaryKey, 1);
                } else {
                    rpcSummary[summaryKey]++;
                }
            }
        }
    }
}
