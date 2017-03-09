using LowLevelDesign.WinTrace.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management.Automation;
using System.Runtime.InteropServices;

namespace LowLevelDesign.WinTrace.PowerShell
{

    [Cmdlet("Invoke", "Wtrace", DefaultParameterSetName = "StartNewProcess")]
    public class InvokeWtraceCommand : PSCmdlet
    {
        ProcessTraceRunner processTraceRunner;

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


        protected override void BeginProcessing()
        {
            if (TraceEventSession.IsElevated() != true) {
                ErrorRecord errorRecord = new ErrorRecord(new InvalidOperationException("Must be elevated (Admin) to run this cmdlet."),
                    "MissingAdminRights", ErrorCategory.InvalidOperation, null);
                WriteError(errorRecord);
                return;
            }
            // FIXME trace output options
            processTraceRunner = new ProcessTraceRunner(TraceOutputOptions.TracesAndSummary);
            try {
                if (string.Equals(ParameterSetName, "StartNewProcess", StringComparison.Ordinal)) {
                    var args = new List<string>() { FilePath };
                    if (ArgumentList != null) {
                        args.AddRange(ArgumentList);
                    }
                    processTraceRunner.TraceNewProcess(args, NewConsole);
                } else {
                    processTraceRunner.TraceRunningProcess(Pid);
                }
            } catch (COMException ex) {
                if ((uint)ex.HResult == 0x800700B7) {
                    Console.Error.WriteLine("ERROR: could not start the kernel logger - make sure it is not running.");
                }
            } catch (Win32Exception ex) {
                ErrorRecord errorRecord = new ErrorRecord(ex, ex.GetType().FullName, ErrorCategory.InvalidOperation, null);
                WriteError(errorRecord);
            } catch (Exception ex) {
                ErrorRecord errorRecord = new ErrorRecord(ex, ex.GetType().FullName, ErrorCategory.InvalidOperation, null);
                WriteError(errorRecord);
            }
        }

        protected override void StopProcessing()
        {
            processTraceRunner.Stop();
        }
    }
}
