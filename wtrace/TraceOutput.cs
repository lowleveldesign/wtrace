using System;

namespace LowLevelDesign.WinTrace
{
    interface ITraceOutput
    {
        void Write(double timeStampRelativeInMSec, int processId, int threadId, string eventName, string details);

        void WriteSummary(string title, string eventsSummary);
    }

    class ConsoleTraceOutput : ITraceOutput
    {
        private readonly string eventNameFilter;

        public ConsoleTraceOutput(string eventNameFilter)
        {
            this.eventNameFilter = eventNameFilter;
        }

        public void Write(double timeStampRelativeInMSec, int processId, int threadId, string eventName, string details)
        {
            if (eventNameFilter == null || 
                eventName.IndexOf(eventNameFilter, StringComparison.OrdinalIgnoreCase) >= 0) {
                Console.WriteLine($"{timeStampRelativeInMSec:0.0000} ({processId}.{threadId}) {eventName} {details}");
            }
        }

        public void WriteSummary(string title, string eventsSummary)
        {
            var separator = "--------------------------------";
            var space = Math.Max(0, (separator.Length - title.Length) / 2);

            Console.WriteLine();
            Console.WriteLine(separator);
            Console.Write("".PadRight(space));
            Console.WriteLine(title);
            Console.WriteLine("--------------------------------");
            Console.WriteLine(eventsSummary);
        }
    }
}
