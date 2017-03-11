using System;

namespace LowLevelDesign.WinTrace
{
    interface ITraceOutput
    {
        void Write(double timeStampRelativeInMSec, int processId, int threadId, string eventName, string details);
    }

    class ConsoleTraceOutput : ITraceOutput
    {
        public void Write(double timeStampRelativeInMSec, int processId, int threadId, string eventName, string details)
        {
            Console.WriteLine($"{timeStampRelativeInMSec:0.0000} ({processId}.{threadId}) {eventName} {details}");
        }
    }
}
