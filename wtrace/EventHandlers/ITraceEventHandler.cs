using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace LowLevelDesign.WinTrace.EventHandlers
{
    public interface ITraceEventHandler
    {
        KernelTraceEventParser.Keywords RequiredKernelFlags { get; }

        void SubscribeToSession(TraceEventSession session); 

        void PrintStatistics(double sessionEndTimeInMs);
    }
}
