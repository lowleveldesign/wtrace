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

            powerShellParser.CommandEvent7937 += OnCommandEvent;
            powerShellParser.CommandEvent4103 += OnCommandEvent;
            powerShellParser.ScriptBlockEvent4104 += OnScriptBlockEvent4104;

            session.EnableProvider(MicrosoftWindowsPowerShellTraceEventParser.ProviderGuid);
        }

        private void OnScriptBlockEvent4104(ScriptBlockEventArgs data)
        {
            if (data.ProcessID == pid) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID,
                    "PowerShell/ScriptBlock", data.ScriptBlockText);
            }
        }

        private void OnCommandEvent(CommandEventArgs data)
        {
            if (data.ProcessID == pid) {
                string commandType = ExtractDataFromContextInfo(data.ContextInfo, "Command Type");
                string eventName = $"PowerShell/{commandType}";

                // It is a very strange way of distinguishing those events, but I could not find a better one
                if ((int)data.ID == 7937 && data.Payload.EndsWith($"Started.{Environment.NewLine}", System.StringComparison.OrdinalIgnoreCase)) {
                    string commandName = ExtractDataFromContextInfo(data.ContextInfo, "Command Name");

                    traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID,
                        eventName, commandName);
                } else if ((int)data.ID == 4103) {
                    traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID,
                        eventName, data.Payload); 
                    /* FIXME better split the commands
CommandInvocation(Set-StrictMode): "Set-StrictMode"
ParameterBinding(Set-StrictMode): name="Off"; value="True"

                or

CommandInvocation(Out-Default): "Out-Default"
ParameterBinding(Out-Default): name="InputObject"; value="books"
ParameterBinding(Out-Default): name="InputObject"; value="debug-recipes"
ParameterBinding(Out-Default): name="InputObject"; value="dev"
ParameterBinding(Out-Default): name="InputObject"; value="diag"
ParameterBinding(Out-Default): name="InputObject"; value="moje"
ParameterBinding(Out-Default): name="InputObject"; value="mybooks"
ParameterBinding(Out-Default): name="InputObject"; value="reference-docs"
ParameterBinding(Out-Default): name="InputObject"; value="repos"
ParameterBinding(Out-Default): name="InputObject"; value="research"
ParameterBinding(Out-Default): name="InputObject"; value="research-archive"
ParameterBinding(Out-Default): name="InputObject"; value="scripts"
ParameterBinding(Out-Default): name="InputObject"; value="shortcuts"
ParameterBinding(Out-Default): name="InputObject"; value="tools
 
                 */

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
