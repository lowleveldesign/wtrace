using Microsoft.Diagnostics.Tracing;
using System;

namespace LowLevelDesign.WinTrace.Handlers
{
    class NetworkTraceEventHandler : ITraceEventHandler
    {
        public string Handle(TraceEvent data)
        {
            throw new NotImplementedException();
        }

        public bool ShouldHandle(TraceEvent data)
        {
            return false;
        }
    }
}
