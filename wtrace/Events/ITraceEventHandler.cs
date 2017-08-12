using Microsoft.Diagnostics.Tracing;

namespace LowLevelDesign.WinTrace.Handlers
{
    public interface ITraceEventHandler
    {
        void SubscribeToEvents(TraceEventParser parser);

        void PrintStatistics(double sessionEndTimeInMs);
    }
}
