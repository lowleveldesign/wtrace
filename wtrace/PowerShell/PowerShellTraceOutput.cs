using System;
using System.Diagnostics;
using System.Management.Automation;

namespace LowLevelDesign.WinTrace.PowerShell
{
    public class PSWtraceEvent
    {
        public double TimeStampRelativeInMSec { get; set; }

        public int ProcessId { get; set; }

        public int ThreadId { get; set; }

        public string EventName { get; set; }

        public string EventDetails { get; set; }
    }

    class PowerShellTraceOutput : ITraceOutput
    {
        private readonly PSCmdlet cmdlet;

        public PowerShellTraceOutput(PSCmdlet cmdlet)
        {
            this.cmdlet = cmdlet;
        }

        public void Write(double timeStampRelativeInMSec, int processId, int threadId, string eventName, string details)
        {
            cmdlet.WriteObject(new PSWtraceEvent {
                TimeStampRelativeInMSec = timeStampRelativeInMSec,
                ProcessId = processId,
                ThreadId = threadId,
                EventName = eventName,
                EventDetails = details
            });
        }
    }

    class PowerShellVerboseTraceListener : TraceListener
    {
        private readonly PSCmdlet cmdlet;

        public PowerShellVerboseTraceListener(PSCmdlet cmdlet)
        {
            this.cmdlet = cmdlet;
        }


        public override void Write(string message)
        {
            cmdlet.WriteVerbose(message);
        }

        public override void WriteLine(string message)
        {
            cmdlet.WriteVerbose(message);
        }
    }
}
