using LowLevelDesign.WinTrace.Handlers;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.IO;
using System.Threading;

namespace LowLevelDesign.WinTrace
{
    enum TraceOutputOptions
    {
        NoSummary,
        OnlySummary,
        TracesAndSummary
    }

    sealed class TraceCollector : IDisposable
    {
        private readonly TraceEventSession session;
        private readonly TextWriter output;

        private bool disposed = false;
        private readonly ITraceEventHandler[] handlers;

        public TraceCollector(int pid, TextWriter output, TraceOutputOptions options)
        {
            this.output = output;
            session = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            session.EnableKernelProvider(
                 KernelTraceEventParser.Keywords.FileIOInit  | KernelTraceEventParser.Keywords.FileIO
                | KernelTraceEventParser.Keywords.NetworkTCPIP
            );
            session.StopOnDispose = true;

            handlers = new ITraceEventHandler[] {
                new SystemConfigTraceEventHandler(output, options),
                new FileIOTraceEventHandler(pid, output, options),
                new NetworkTraceEventHandler(pid, output, options),
                new ProcessThreadsTraceEventHandler(pid, output, options), 
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
