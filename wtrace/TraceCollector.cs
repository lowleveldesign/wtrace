using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.IO;
using System.Text;

namespace wtrace
{
    class TraceCollector : IDisposable
    {
        private readonly TraceEventSession session;
        private readonly TextWriter output;
        private readonly StringBuilder buffer = new StringBuilder(2000, 2000);
        private readonly int pid;

        private bool disposed = false;

        public TraceCollector(string sessionName, int pid, TextWriter output)
        {
            session = new TraceEventSession(sessionName);
            session.EnableKernelProvider(KernelTraceEventParser.Keywords.FileIOInit);
            session.Source.Kernel.All += ProcessTraceEvent;

            this.pid = pid;
            this.output = output;
        }

        void ProcessTraceEvent(TraceEvent data)
        {
            // There are a lot of data collection start on entry that I don't want to see (but often they are quite handy
            if (data.Opcode == TraceEventOpcode.DataCollectionStart || data.Opcode == TraceEventOpcode.DataCollectionStop)
                return;

            if (data.ProcessID == pid) {

                //if (data is FileIOOpEndTraceData) {
                //    // we don't care about operation result
                //    return;
                //}

                buffer.AppendFormat("Event '{0}', process: {1}\n", data.EventName, data.ProcessID);
                //if (buffer.MaxCapacity - buffer.Length < 100) {
                    output.WriteLine(buffer.ToString());
                    buffer.Clear();
                //}
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
