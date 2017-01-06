using LowLevelDesign.WinTrace.Handlers;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System.IO;

namespace LowLevelDesign.WinTrace.Tracing
{
    sealed class KernelTraceCollector : TraceCollector
    {
        public KernelTraceCollector(int pid, TextWriter output, TraceOutputOptions options)
            : base(CreateKernelTraceEventSession(), output)
        {
            eventHandlers.Add(new SystemConfigTraceEventHandler(output, options));
            eventHandlers.Add(new FileIOTraceEventHandler(pid, output, options));
            eventHandlers.Add(new NetworkTraceEventHandler(pid, output, options));
            eventHandlers.Add(new ProcessThreadsTraceEventHandler(pid, output, options));
            eventHandlers.Add(new AlpcTraceEventHandler(pid, output, options));

            foreach (var handler in eventHandlers) {
                handler.SubscribeToEvents(traceSession.Source.Kernel);
            }
        }

        static TraceEventSession CreateKernelTraceEventSession()
        {
            var kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName) {
                StopOnDispose = true
            };
            kernelSession.EnableKernelProvider(
                 KernelTraceEventParser.Keywords.FileIOInit  | KernelTraceEventParser.Keywords.FileIO
                | KernelTraceEventParser.Keywords.NetworkTCPIP | KernelTraceEventParser.Keywords.AdvancedLocalProcedureCalls
            );

            return kernelSession;
        }
    }
}
