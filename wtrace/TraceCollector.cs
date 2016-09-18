using LowLevelDesign.WinTrace.Handlers;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
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
        private readonly TextWriter output;
        private readonly StringBuilder buffer = new StringBuilder(2000, 2000);
        private readonly int pid;
        private readonly ITraceEventHandler[] handlers;

        private bool disposed = false;

        public TraceCollector(string sessionName, int pid, TextWriter output)
        {
            session = new TraceEventSession(sessionName);
            session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.Thread
                | KernelTraceEventParser.Keywords.FileIOInit  | KernelTraceEventParser.Keywords.FileIO
                | KernelTraceEventParser.Keywords.NetworkTCPIP
                //| KernelTraceEventParser.Keywords.Registry
            );
            session.Source.Kernel.All += ProcessTraceEvent;

            this.pid = pid;
            this.output = output;
            handlers = new ITraceEventHandler[] {
                new FileIOTraceEventHandler(),
                new RegistryTraceEventHandler(),
                new NetworkTraceEventHandler()
            };
        }

        void ProcessTraceEvent(TraceEvent data)
        {
            // There are a lot of data collection start on entry that I don't want to see (but often they are quite handy
            if (data.Opcode == TraceEventOpcode.DataCollectionStart || data.Opcode == TraceEventOpcode.DataCollectionStop)
                return;

            if (data.ProcessID == pid) {
                foreach (var handler in handlers) {
                    if (handler.ShouldHandle(data)) {
                        var result = handler.Handle(data);

                        if (result.Length > 0) {
                            result += Environment.NewLine;
                            if (buffer.MaxCapacity - buffer.Length < result.Length) {
                                output.Write(buffer.ToString());
                                buffer.Clear();
                            }
                            buffer.Append(result);
                        }
                        return;
                    }
                }
            }
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
