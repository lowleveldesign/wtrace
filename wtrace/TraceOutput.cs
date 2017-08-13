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
        public void Write(double timeStampRelativeInMSec, int processId, int threadId, string eventName, string details)
        {
            Console.WriteLine($"{timeStampRelativeInMSec:0.0000} ({processId}.{threadId}) {eventName} {details}");
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
