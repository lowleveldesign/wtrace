using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;

namespace LowLevelDesign.WinTrace.Handlers
{
    class RegistryTraceEventHandler : ITraceEventHandler
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
