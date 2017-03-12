using LowLevelDesign.WinTrace.Handlers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace LowLevelDesign.WinTrace.Tracing
{
    abstract class TraceCollector : IDisposable
    {
        private bool disposed = false;

        protected readonly TraceEventSession traceSession;
        protected readonly List<ITraceEventHandler> eventHandlers;
        protected readonly Stopwatch sw = new Stopwatch();

        public TraceCollector(TraceEventSession session)
        {
            traceSession = session;
            eventHandlers = new List<ITraceEventHandler>();
        }

        public void Start()
        {
            sw.Start();
            traceSession.Source.Process();
        }

        public void Stop(bool printSummary)
        {
            if (traceSession.IsActive) {
                int eventsLost = traceSession.EventsLost;

                Trace.WriteLine($"### Stopping {traceSession.SessionName} session...");
                traceSession.Stop();

                sw.Stop();

                // This timeout is needed to handle all the DCStop events 
                // (in case we ever are going to do anything about them)
                Thread.Sleep(1500);

                Trace.WriteLine($"### {traceSession.SessionName} session stopped. Number of lost events: {eventsLost:#,0}");

                if (printSummary) {
                    foreach (var handler in eventHandlers) {
                        handler.PrintStatistics(sw.Elapsed.TotalMilliseconds);
                    }
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
