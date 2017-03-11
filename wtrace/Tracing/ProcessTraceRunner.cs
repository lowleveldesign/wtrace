using System;
using System.Collections.Generic;
using System.Threading;
using WinHandles = VsChromium.Core.Win32.Handles;
using WinProcesses = VsChromium.Core.Win32.Processes;

namespace LowLevelDesign.WinTrace.Tracing
{
    class ProcessTraceRunner
    {
        private readonly ManualResetEvent stopEvent = new ManualResetEvent(false);
        private readonly bool printSummary;
        private readonly ITraceOutput traceOutput;
        private Action<bool> stopTraceCollectors;

        public ProcessTraceRunner(ITraceOutput traceOutput, bool printSummary)
        {
            this.traceOutput = traceOutput;
            this.printSummary = printSummary;
        }

        public void TraceNewProcess(IEnumerable<string> procargs, bool spawnNewConsoleWindow)
        {
            using (var process = new ProcessCreator(procargs) { SpawnNewConsoleWindow = spawnNewConsoleWindow }) {
                process.StartSuspended();

                using (TraceCollector kernelTraceCollector = new KernelTraceCollector(process.ProcessId, traceOutput),
                    userTraceCollector = new UserTraceCollector(process.ProcessId, traceOutput)) {

                    ThreadPool.QueueUserWorkItem((o) => {
                        process.Join();
                        kernelTraceCollector.Stop(printSummary);
                        userTraceCollector.Stop(printSummary);

                        stopEvent.Set();
                    });

                    stopTraceCollectors = (bool overridenPrintSummary) => {
                        kernelTraceCollector.Stop(overridenPrintSummary);
                        userTraceCollector.Stop(overridenPrintSummary);
                    };

                    ThreadPool.QueueUserWorkItem((o) => {
                        kernelTraceCollector.Start();
                    });
                    ThreadPool.QueueUserWorkItem((o) => {
                        userTraceCollector.Start();
                    });

                    Thread.Sleep(1000);

                    // resume thread
                    process.Resume();

                    stopEvent.WaitOne();
                }
            }
        }

        public void TraceRunningProcess(int pid)
        {
            using (var hProcess = WinProcesses.NativeMethods.OpenProcess(WinProcesses.ProcessAccessFlags.Synchronize, false, pid)) {
                if (hProcess.IsInvalid) {
                    Console.Error.WriteLine("ERROR: the process with a given PID was not found or you don't have access to it.");
                    return;
                }
                using (TraceCollector kernelTraceCollector = new KernelTraceCollector(pid, traceOutput),
                    userTraceCollector = new UserTraceCollector(pid, traceOutput)) {

                    ThreadPool.QueueUserWorkItem((o) => {
                        WinHandles.NativeMethods.WaitForSingleObject(hProcess, VsChromium.Core.Win32.Constants.INFINITE);
                        kernelTraceCollector.Stop(printSummary);
                        userTraceCollector.Stop(printSummary);

                        stopEvent.Set();
                    });

                    stopTraceCollectors = (bool overridenPrintSummary) => {
                        kernelTraceCollector.Stop(overridenPrintSummary);
                        userTraceCollector.Stop(overridenPrintSummary);
                    };

                    ThreadPool.QueueUserWorkItem((o) => {
                        kernelTraceCollector.Start();
                    });
                    ThreadPool.QueueUserWorkItem((o) => {
                        userTraceCollector.Start();
                    });

                    stopEvent.WaitOne();
                }
            }
        }

        public void Stop()
        {
            if (stopTraceCollectors != null) {
                stopTraceCollectors(printSummary);
                stopTraceCollectors = null;
            }

            stopEvent.Set();
        }

        public void Stop(bool overridenPrintSummary)
        {
            if (stopTraceCollectors != null) {
                stopTraceCollectors(overridenPrintSummary);
                stopTraceCollectors = null;
            }

            stopEvent.Set();
        }
    }
}
