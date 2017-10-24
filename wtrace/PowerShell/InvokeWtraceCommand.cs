using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;

namespace LowLevelDesign.WinTrace.PowerShell
{

    [Cmdlet("Invoke", "Wtrace", DefaultParameterSetName = "StartNewProcess")]
    [OutputType(typeof(PowerShellWtraceEvent))]
    public class InvokeWtraceCommand : PSCmdlet
    {
        readonly ConcurrentQueue<PowerShellWtraceEvent> eventQueue = new ConcurrentQueue<PowerShellWtraceEvent>();
        TraceSession processTraceRunner;

        [Parameter(HelpMessage = "Defines whether events statistics will be printed at the end of the trace.")]
        public bool NoSummary { get; set; }

        [Parameter(
            Mandatory = true,
            ParameterSetName = "StartNewProcess",
            Position = 0,
            HelpMessage = "The file path of the executable to start.")]
        [Alias("PSPath")]
        [ValidateNotNullOrEmpty]
        public string FilePath { get; set; }

        [Parameter(
            Mandatory = false,
            ParameterSetName = "StartNewProcess",
            Position = 1,
            HelpMessage = "Optional arguments to the executable.")]
        [Alias("Args")]
        [ValidateNotNullOrEmpty]
        public string[] ArgumentList { get; set; }

        [Parameter(
            Mandatory = false,
            ParameterSetName = "StartNewProcess",
            HelpMessage = "Start the process in a new console window.")]
        public bool NewConsole { get; set; }

        [Parameter(
            Mandatory = true,
            ParameterSetName = "AttachToExistingProcess",
            Position = 0,
            HelpMessage = "Id of the process, which you would like to trace.",
            ValueFromPipelineByPropertyName = true
            )]
        [Alias("Id")]
        public int Pid { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Display only events which names contain the given keyword (case insensitive).")]
        public string Filter { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Trace child processes.")]
        public bool TraceChildProcesses { get; set; }

        protected override void BeginProcessing()
        {
        }

        protected override void ProcessRecord()
        {
            ErrorRecord errorRecord = null;
            if (TraceEventSession.IsElevated() != true) {
                errorRecord = new ErrorRecord(new InvalidOperationException("Must be elevated (Admin) to run this cmdlet."),
                    "MissingAdminRights", ErrorCategory.InvalidOperation, null);
                WriteError(errorRecord);
                return;
            }

            processTraceRunner = new TraceSession(new PowerShellTraceOutput(eventQueue, Filter), !NoSummary);
            bool isMainThreadFinished = false;
            const bool collectSystemStats = false; // not available in PowerShell

            ThreadPool.QueueUserWorkItem((o) => {
                try {
                    if (string.Equals(ParameterSetName, "StartNewProcess", StringComparison.Ordinal)) {
                        var args = new List<string>() { FilePath };
                        if (ArgumentList != null) {
                            args.AddRange(ArgumentList);
                        }
                        processTraceRunner.TraceNewProcess(args, NewConsole, TraceChildProcesses, 
                            collectSystemStats);
                    } else {
                        processTraceRunner.TraceRunningProcess(Pid, TraceChildProcesses, collectSystemStats);
                    }
                    isMainThreadFinished = true;
                } catch (Exception ex) {
                    errorRecord = new ErrorRecord(ex, ex.GetType().FullName, ErrorCategory.InvalidOperation, null);
                    isMainThreadFinished = true;
                }
            });

            PowerShellWtraceEvent ev;
            while (!isMainThreadFinished) {
                while (eventQueue.TryDequeue(out ev)) {
                    WriteObject(ev);
                }
                Thread.Sleep(100);
            }
            // the rest of the events
            while (eventQueue.TryDequeue(out ev)) {
                WriteObject(ev);
            }

            if (errorRecord != null) {
                WriteError(errorRecord);
            }
        }

        protected override void StopProcessing()
        {
            processTraceRunner.Stop(false);
        }
    }
}
