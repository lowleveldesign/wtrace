using LowLevelDesign.WinTrace.Tracing;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsRPC;
using System;
using System.IO;

namespace LowLevelDesign.WinTrace.Handlers
{
    sealed class RpcTraceEventHandler : ITraceEventHandler
    {
        private readonly TextWriter summaryOutput;
        private readonly TextWriter traceOutput;
        private readonly int pid;

        public RpcTraceEventHandler(int pid, TextWriter output, TraceOutputOptions options)
        {
            summaryOutput = options == TraceOutputOptions.NoSummary ? TextWriter.Null : output;
            traceOutput = options == TraceOutputOptions.OnlySummary ? TextWriter.Null : output;
            this.pid = pid;
        }

        public void PrintStatistics()
        {
            // FIXME
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

        private void RpcServerCallStart(RpcServerCallStartArgs ev)
        {
            if (ev.ProcessID == pid) {
                Console.WriteLine(ev.Dump(true, true));
            }
        }

        private void RpcClientCallStop(RpcClientCallStopArgs ev)
        {
        }

        private void RpcClientCallStart(RpcClientCallStartArgs ev)
        {
            if (ev.ProcessID == pid) {
                Console.WriteLine(ev.Dump(true, true));
            }
        }
    }
}
