using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace LowLevelDesign.WinTrace.Handlers
{
    public interface ITraceEventHandler
    {
        void SubscribeToEvents(KernelTraceEventParser kernel);
    }
}
