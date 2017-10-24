using System;
using System.Collections.Concurrent;

namespace LowLevelDesign.WinTrace.PowerShell
{
    public class PowerShellWtraceEvent
    {
        public double TimeStampRelativeInMSec { get; set; }

        public int ProcessId { get; set; }

        public int ThreadId { get; set; }

        public string EventName { get; set; }

        public string EventDetails { get; set; }
    }

    class PowerShellTraceOutput : ITraceOutput
    {
        private readonly string eventNameFilter;
        private readonly ConcurrentQueue<PowerShellWtraceEvent> eventQueue;

        public PowerShellTraceOutput(ConcurrentQueue<PowerShellWtraceEvent> eventQueue, string eventNameFilter)
        {
            this.eventQueue = eventQueue;
            this.eventNameFilter = eventNameFilter;
        }

        public void Write(double timeStampRelativeInMSec, int processId, int threadId, string eventName, string details)
        {
            if (eventNameFilter == null ||
                eventName.IndexOf(eventNameFilter, StringComparison.OrdinalIgnoreCase) >= 0) {
                eventQueue.Enqueue(new PowerShellWtraceEvent {
                    TimeStampRelativeInMSec = timeStampRelativeInMSec,
                    ProcessId = processId,
                    ThreadId = threadId,
                    EventName = eventName,
                    EventDetails = details
                });
            }
        }

        public void WriteSummary(string title, string eventsSummary)
        {
        }
    }
}
