using LowLevelDesign.WinTrace.Handlers;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.IO;
using System.Threading;

namespace LowLevelDesign.WinTrace
{
    class TraceCollector : IDisposable
    {
        private readonly TraceEventSession session;
        private readonly TextWriter output;

        private bool disposed = false;
        private readonly ITraceEventHandler[] handlers;

        public TraceCollector(int pid, TextWriter output)
        {
            this.output = output;
            session = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            session.EnableKernelProvider(
                 KernelTraceEventParser.Keywords.FileIOInit  | KernelTraceEventParser.Keywords.FileIO
                | KernelTraceEventParser.Keywords.NetworkTCPIP
#if REGISTRY
                | KernelTraceEventParser.Keywords.Registry
#endif
            );
            session.StopOnDispose = true;

            handlers = new ITraceEventHandler[] {
#if REGISTRY
                new RegistryTraceEventHandler(pid, output),
#endif
                new SystemConfigTraceEventHandler(output),
                new FileIOTraceEventHandler(pid, output),
                new NetworkTraceEventHandler(pid, output)
            };
            foreach (var handler in handlers) {
                handler.SubscribeToEvents(session.Source.Kernel);
            }
        }

        public void Start()
        {
            session.Source.Process();
        }

        public void Stop()
        {
            if (session.IsActive) {
                int eventsLost = session.EventsLost;

                output.WriteLine("### Stopping ETW session...");
                session.Stop();

                // This timeout is needed to handle all the DCStop events 
                // (in case we ever are going to do anything about them)
                Thread.Sleep(1500);

                output.WriteLine("======= ETW session =======");
                output.WriteLine($"### ETW session stopped. Number of lost events: {eventsLost:#,0}");

                foreach (var handler in handlers) {
                    handler.PrintStatistics();
                }
            }
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
