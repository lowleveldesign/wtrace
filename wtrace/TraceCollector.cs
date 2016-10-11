using LowLevelDesign.WinTrace.Handlers;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace LowLevelDesign.WinTrace
{
    class TraceCollector : IDisposable
    {
        private readonly TraceEventSession session;
        private readonly StringBuilder buffer = new StringBuilder(2000, 2000);
        private readonly ITraceEventHandler[] handlers;

        private bool disposed = false;

        public TraceCollector(int pid, TextWriter output)
        {
            session = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.Thread
                | KernelTraceEventParser.Keywords.FileIOInit  | KernelTraceEventParser.Keywords.FileIO
                | KernelTraceEventParser.Keywords.NetworkTCPIP
                | KernelTraceEventParser.Keywords.Registry
            );

            new FileIOTraceEventHandler(pid, output).SubscribeToEvents(session.Source.Kernel);
                //new RegistryTraceEventHandler(),
                //new NetworkTraceEventHandler()
        }

        public void Start()
        {
            session.Source.Process();
        }

        public void Dispose()
        {
            if (disposed) {
                return;
            }
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (session.IsActive) {
                session.Stop();
                // This 1s timeout is needed to handle all the DCStop events 
                // (in case we ever are going to do anything about them)
                Thread.Sleep(1000);
            }
            if (disposing) {
                session.Dispose();
            }
            disposed = true;
        }

        ~TraceCollector()
        {
            if (disposed) {
                return;
            }
            Dispose(false);
        }
    }
}
