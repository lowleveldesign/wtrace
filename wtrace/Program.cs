using Microsoft.Diagnostics.Tracing.Session;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using WinProcesses = VsChromium.Core.Win32.Processes;
using WinHandles = VsChromium.Core.Win32.Handles;

namespace LowLevelDesign.WinTrace
{
    static class Program
    {
        static ManualResetEvent stopEvent = new ManualResetEvent(false);

        public static void Main(string[] args)
        {
            if (TraceEventSession.IsElevated() != true) {
                Console.Error.WriteLine("Must be elevated (Admin) to run this program.");
                return;
            }

            List<string> procargs = null;
            bool showhelp = false, spawnNewConsoleWindow = false, 
                summaryOnly = false;
            int pid = 0;

            var p = new OptionSet
            {
                { "newconsole", "Start the process in a new console window.", v => { spawnNewConsoleWindow = v != null; } },
                { "summary", "Prints only a summary of the collected trace.", v => summaryOnly = v != null },
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

            if (!int.TryParse(procargs[0], out pid)) {
                TraceNewProcess(procargs, spawnNewConsoleWindow, summaryOnly);
            } else {
                TraceRunningProcess(pid, summaryOnly);
            }
        }

        static void TraceNewProcess(IEnumerable<string> procargs, bool spawnNewConsoleWindow, bool summaryOnly)
        {
            using (var process = new ProcessCreator(procargs) { SpawnNewConsoleWindow = spawnNewConsoleWindow }) {
                process.StartSuspended();

                using (var collector = new TraceCollector(process.ProcessId, Console.Out, summaryOnly)) {
                    SetConsoleCtrlCHook(collector);

                    ThreadPool.QueueUserWorkItem((o) => {
                        collector.Start();
                    });
                    ThreadPool.QueueUserWorkItem((o) => {
                        process.Join();
                        collector.Stop();

                        stopEvent.Set();
                    });

                    Thread.Sleep(1000);

                    // resume thread
                    process.Resume();

                    stopEvent.WaitOne();
                }
            }
        }

        static void TraceRunningProcess(int pid, bool summaryOnly)
        {
            var hProcess = WinProcesses.NativeMethods.OpenProcess(WinProcesses.ProcessAccessFlags.Synchronize, false, pid);
            if (hProcess.IsInvalid) {
                Console.Error.WriteLine("ERROR: the process with a given PID was not found or you don't have access to it.");
                return;
            }
            using (var collector = new TraceCollector(pid, Console.Out, summaryOnly)) {
                SetConsoleCtrlCHook(collector);
                ThreadPool.QueueUserWorkItem((o) => {
                    WinHandles.NativeMethods.WaitForSingleObject(hProcess, VsChromium.Core.Win32.Constants.INFINITE);
                    collector.Stop();

                    stopEvent.Set();
                });
                collector.Start();

                stopEvent.WaitOne();
            }
        }

        static void SetConsoleCtrlCHook(TraceCollector collector)
        {
            // Set up Ctrl-C to stop both user mode and kernel mode sessions
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) => {
                cancelArgs.Cancel = true;
                collector.Stop();

                stopEvent.Set();
            };
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("wtrace v{0} - collects traces of Windows processes",
                Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Console.WriteLine("Copyright (C) 2016 Sebastian Solnica (@lowleveldesign)");
            Console.WriteLine();
            Console.WriteLine("Usage: wtrace [OPTIONS] args");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
        }

    }
}
