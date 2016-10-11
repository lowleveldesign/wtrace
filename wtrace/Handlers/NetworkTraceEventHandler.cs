using Microsoft.Diagnostics.Tracing;
using System;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace LowLevelDesign.WinTrace.Handlers
{
    class NetworkTraceEventHandler : ITraceEventHandler
    {
        public void SubscribeToEvents(KernelTraceEventParser kernel)
        {
            throw new NotImplementedException();
        }
    }
}
