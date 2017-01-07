using LowLevelDesign.WinTrace.Handlers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace LowLevelDesign.WinTrace.Tracing
{
    enum TraceOutputOptions
    {
        NoSummary,
        OnlySummary,
        TracesAndSummary
    }

    abstract class TraceCollector : IDisposable
    {
        private bool disposed = false;

        protected readonly TextWriter output;
        protected readonly TraceEventSession traceSession;
        protected readonly List<ITraceEventHandler> eventHandlers;

        public TraceCollector(TraceEventSession session, TextWriter output)
        {
            this.output = output;
            this.traceSession = session;
            this.eventHandlers = new List<ITraceEventHandler>();
        }

        public void Start()
        {
            traceSession.Source.Process();
        }

        public void Stop()
        {
            if (traceSession.IsActive) {
                int eventsLost = traceSession.EventsLost;

                output.WriteLine($"### Stopping {traceSession.SessionName} session...");
                traceSession.Stop();

                // This timeout is needed to handle all the DCStop events 
                // (in case we ever are going to do anything about them)
                Thread.Sleep(1500);

                output.WriteLine($"### {traceSession.SessionName} session stopped. Number of lost events: {eventsLost:#,0}");
                output.WriteLine();

                foreach (var handler in eventHandlers) {
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
                traceSession.Dispose();
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
