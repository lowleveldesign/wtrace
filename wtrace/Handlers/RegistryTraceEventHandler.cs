using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace LowLevelDesign.WinTrace.Handlers
{
    class RegistryTraceEventHandler : ITraceEventHandler
    {
        public void SubscribeToEvents(KernelTraceEventParser kernel)
        {
            throw new NotImplementedException();
        }
    }
}
