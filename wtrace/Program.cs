using LowLevelDesign.WinTrace.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Utilities;
using WinHandles = VsChromium.Core.Win32.Handles;
using WinProcesses = VsChromium.Core.Win32.Processes;

namespace LowLevelDesign.WinTrace
{
    static class Program
    {
        static ManualResetEvent stopEvent = new ManualResetEvent(false);

        [STAThread()]
        public static void Main(string[] args)
        {
            Unpack();

            DoMain(args);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void DoMain(string[] args)
        {
            if (TraceEventSession.IsElevated() != true) {
                Console.Error.WriteLine("Must be elevated (Admin) to run this program.");
                return;
            }

            List<string> procargs = null;
            bool showhelp = false, spawnNewConsoleWindow = false;
            TraceOutputOptions options = TraceOutputOptions.TracesAndSummary;

            int pid = 0;

            var p = new OptionSet
            {
                { "newconsole", "Start the process in a new console window.", v => { spawnNewConsoleWindow = v != null; } },
                { "summary", "Prints only a summary of the collected trace.", v => {
                    if (v != null) {
                        options = TraceOutputOptions.OnlySummary;
                    }
                } },
                { "nosummary", "Prints only ETW events - no summary at the end.", v => {
                    if (v != null) {
                        options = TraceOutputOptions.NoSummary;                        
                    }

                } },
                { "h|help", "Show this message and exit", v => showhelp = v != null },
                { "?", "Show this message and exit", v => showhelp = v != null }
            };

            try {
                procargs = p.Parse(args);
            } catch (OptionException ex) {
                Console.Error.Write("ERROR: invalid argument");
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine();
                showhelp = true;
            } catch (FormatException) {
                Console.Error.WriteLine("ERROR: invalid number in one of the constraints");
                Console.Error.WriteLine();
                showhelp = true;
            }

            Debug.Assert(procargs != null);
            if (!showhelp && procargs.Count == 0) {
                Console.Error.WriteLine("ERROR: please provide either process name or PID of the already running process");
                Console.Error.WriteLine();
                showhelp = true;
            }

            if (showhelp) {
                ShowHelp(p);
                return;
            }

            try {
                if (!int.TryParse(procargs[0], out pid)) {
                    TraceNewProcess(procargs, spawnNewConsoleWindow, options);
                }
                else {
                    TraceRunningProcess(pid, options);
                }
            }
            catch (COMException ex) {
                if ((uint) ex.HResult == 0x800700B7) {
                    Console.Error.WriteLine("ERROR: could not start the kernel logger - make sure it is not running.");
                }
            }
            catch (Win32Exception ex) {
                Console.Error.WriteLine(
                    $"ERROR: an error occurred while trying to start or open the process, code: 0x{ex.HResult:X}");
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"ERROR: severe error happened when starting application: {ex.Message}");
            }
        }

        static void TraceNewProcess(IEnumerable<string> procargs, bool spawnNewConsoleWindow, TraceOutputOptions options)
        {
            using (var process = new ProcessCreator(procargs) { SpawnNewConsoleWindow = spawnNewConsoleWindow }) {
                process.StartSuspended();

                using (TraceCollector kernelTraceCollector = new KernelTraceCollector(process.ProcessId, Console.Out, options),
                    userTraceCollector = new UserTraceCollector(process.ProcessId, Console.Out, options)) {
                    SetConsoleCtrlCHook(kernelTraceCollector, userTraceCollector);

                    ThreadPool.QueueUserWorkItem((o) => {
                        kernelTraceCollector.Start();
                    });
                    ThreadPool.QueueUserWorkItem((o) => {
                        userTraceCollector.Start();
                    });
                    ThreadPool.QueueUserWorkItem((o) => {
                        process.Join();
                        kernelTraceCollector.Stop();
                        userTraceCollector.Stop();

                        stopEvent.Set();
                    });

                    Thread.Sleep(1000);

                    // resume thread
                    process.Resume();

                    stopEvent.WaitOne();
                }
            }
        }

        static void TraceRunningProcess(int pid, TraceOutputOptions options)
        {
            var hProcess = WinProcesses.NativeMethods.OpenProcess(WinProcesses.ProcessAccessFlags.Synchronize, false, pid);
            if (hProcess.IsInvalid) {
                Console.Error.WriteLine("ERROR: the process with a given PID was not found or you don't have access to it.");
                return;
            }
            using (TraceCollector kernelTraceCollector = new KernelTraceCollector(pid, Console.Out, options),
                userTraceCollector = new UserTraceCollector(pid, Console.Out, options)) {
                SetConsoleCtrlCHook(kernelTraceCollector, userTraceCollector);

                ThreadPool.QueueUserWorkItem((o) => {
                    WinHandles.NativeMethods.WaitForSingleObject(hProcess, VsChromium.Core.Win32.Constants.INFINITE);
                    kernelTraceCollector.Stop();
                    userTraceCollector.Stop();

                    stopEvent.Set();
                });

                ThreadPool.QueueUserWorkItem((o) => {
                    kernelTraceCollector.Start();
                });
                ThreadPool.QueueUserWorkItem((o) => {
                    userTraceCollector.Start();
                });

                stopEvent.WaitOne();
            }
        }

        static void SetConsoleCtrlCHook(TraceCollector kernelCollector, TraceCollector userCollector)
        {
            // Set up Ctrl-C to stop both user mode and kernel mode sessions
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) => {
                cancelArgs.Cancel = true;
                kernelCollector.Stop();
                userCollector.Stop();

                stopEvent.Set();
            };
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("wtrace v{0} - collects traces of Windows processes",
                Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Console.WriteLine("Copyright (C) 2016 Sebastian Solnica (@lowleveldesign)");
            Console.WriteLine();
            Console.WriteLine("Usage: wtrace [OPTIONS] pid|imagename args");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
        }

        /// <summary>
        /// Unpacks all the support files associated with this program.   
        /// </summary>
        public static bool Unpack()
        {
            return SupportFiles.UnpackResourcesIfNeeded();
        }
    }
}
