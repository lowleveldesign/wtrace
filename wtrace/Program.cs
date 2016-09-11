using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wtrace
{
    class Program
    {
        static void Main(string[] args)
        {
            if (TraceEventSession.IsElevated() != true)
            {
                Console.WriteLine("Must be elevated (Admin) to run this program.");
                return;
            }

            TraceCollector collector = new TraceCollector("wtrace-session", Console.Out);

            // Set up Ctrl-C to stop both user mode and kernel mode sessions
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) =>
            {
                collector.Dispose();
                cancelArgs.Cancel = true;
            };

            try {
                collector.Start();
            } finally {
                collector.Dispose();
            }
        }
    }
}
