using Microsoft.Diagnostics.Tracing;
using System.Text;

namespace LowLevelDesign.WinTrace.Handlers
{
    public interface ITraceEventHandler
    {
        bool ShouldHandle(TraceEvent data);

        void Handle(TraceEvent data, StringBuilder output);
    }
}
