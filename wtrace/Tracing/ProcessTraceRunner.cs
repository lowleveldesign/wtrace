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
        private readonly TraceOutputOptions traceOutputOptions;
        private Action stopTraceCollectors;

        public ProcessTraceRunner(TraceOutputOptions traceOutputOptions)
        {
            this.traceOutputOptions = traceOutputOptions;
        }

        public void TraceNewProcess(IEnumerable<string> procargs, bool spawnNewConsoleWindow)
        {
            using (var process = new ProcessCreator(procargs) { SpawnNewConsoleWindow = spawnNewConsoleWindow }) {
                process.StartSuspended();

                using (TraceCollector kernelTraceCollector = new KernelTraceCollector(process.ProcessId, Console.Out, traceOutputOptions),
                    userTraceCollector = new UserTraceCollector(process.ProcessId, Console.Out, traceOutputOptions)) {

                    ThreadPool.QueueUserWorkItem((o) => {
                        process.Join();
                        kernelTraceCollector.Stop();
                        userTraceCollector.Stop();

                        stopEvent.Set();
                    });

                    stopTraceCollectors = () => {
                        kernelTraceCollector.Stop();
                        userTraceCollector.Stop();
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
                using (TraceCollector kernelTraceCollector = new KernelTraceCollector(pid, Console.Out, traceOutputOptions),
                    userTraceCollector = new UserTraceCollector(pid, Console.Out, traceOutputOptions)) {

                    ThreadPool.QueueUserWorkItem((o) => {
                        WinHandles.NativeMethods.WaitForSingleObject(hProcess, VsChromium.Core.Win32.Constants.INFINITE);
                        kernelTraceCollector.Stop();
                        userTraceCollector.Stop();

                        stopEvent.Set();
                    });

                    stopTraceCollectors = () => {
                        kernelTraceCollector.Stop();
                        userTraceCollector.Stop();
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
                stopTraceCollectors();
                stopTraceCollectors = null;
            }

            stopEvent.Set();
        }
    }
}
