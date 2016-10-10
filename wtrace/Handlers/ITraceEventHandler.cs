using Microsoft.Diagnostics.Tracing;

namespace LowLevelDesign.WinTrace.Handlers
{
    public interface ITraceEventHandler
    {
        bool ShouldHandle(TraceEvent data);

        string Handle(TraceEvent data);
    }
}
