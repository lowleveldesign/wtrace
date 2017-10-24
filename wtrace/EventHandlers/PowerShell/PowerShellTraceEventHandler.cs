using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsPowerShell;
using System;

namespace LowLevelDesign.WinTrace.EventHandlers.PowerShell
{
    sealed class PowerShellTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;

        public KernelTraceEventParser.Keywords RequiredKernelFlags => KernelTraceEventParser.Keywords.None;

        public PowerShellTraceEventHandler(int pid, ITraceOutput output)
        {
            traceOutput = output;
            this.pid = pid;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
            // We won't track statistics for the PowerShell events
        }

        public void SubscribeToSession(TraceEventSession session)
        {
            var powerShellParser = new MicrosoftWindowsPowerShellTraceEventParser(session.Source);

            powerShellParser.CommandEvent += OnCommandEvent;
            powerShellParser.ScriptBlockEvent += OnScriptBlockEvent;

            session.EnableProvider(MicrosoftWindowsPowerShellTraceEventParser.ProviderGuid);
        }

        private void OnScriptBlockEvent(ScriptBlockEventArgs data)
        {
            if (data.ProcessID == pid) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, 
                    "PowerShell/ScriptBlock", data.ScriptBlockText);
            }
        }

        private void OnCommandEvent(CommandEventArgs data)
        {
            if (data.ProcessID == pid) {
                // It is a very strange way of distinguishing those events, but I could not find a better one
                if (data.Payload.EndsWith($"Started.{Environment.NewLine}", System.StringComparison.OrdinalIgnoreCase)) {
                    string commandName = ExtractDataFromContextInfo(data.ContextInfo, "Command Name");
                    string commandType = ExtractDataFromContextInfo(data.ContextInfo, "Command Type");

                    string eventName = $"PowerShell/{commandType}";
                    traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, 
                        eventName, commandName);
                }
            }
        }

        private string ExtractDataFromContextInfo(string contextInfo, string label)
        {
            label = $" {label} = ";
            int startIndex = contextInfo.IndexOf(label, StringComparison.Ordinal);
            if (startIndex >= 0) {
                startIndex += label.Length;
                var endIndex = contextInfo.IndexOf(Environment.NewLine, startIndex, StringComparison.Ordinal);
                if (endIndex - startIndex > 1) {
                    return contextInfo.Substring(startIndex, endIndex - startIndex);
                }
            }
            return null;
        }
    }
}
