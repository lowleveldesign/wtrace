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
        public static void Main(string[] args)
        {
            if (TraceEventSession.IsElevated() != true) {
                Console.Error.WriteLine("Must be elevated (Admin) to run this program.");
                return;
            }

            List<string> procargs = null;
            bool showhelp = false, spawnNewConsoleWindow = false;
            int pid = 0;

            var p = new OptionSet()
            {
                    { "p|pid=", "Attach to an already running process", (int v) => pid = v },
                    { "newconsole", "Start the process in a new console window.", v => { spawnNewConsoleWindow = v != null; } },
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
            if (!showhelp && (procargs.Count == 0 && pid == 0) || (pid > 0 && procargs.Count > 0)) {
                Console.Error.WriteLine("ERROR: please provide either process name or PID of the already running process");
                Console.Error.WriteLine();
                showhelp = true;
            }

            if (showhelp) {
                ShowHelp(p);
                return;
            }

            if (pid == 0) {
                TraceNewProcess(procargs, spawnNewConsoleWindow);
            } else {
                TraceRunningProcess(pid);
            }
        }

        static void TraceNewProcess(IEnumerable<string> procargs, bool spawnNewConsoleWindow)
        {
            using (var process = new ProcessCreator(procargs) { SpawnNewConsoleWindow = spawnNewConsoleWindow }) {
                process.StartSuspended();

                using (var collector = new TraceCollector(process.ProcessId, Console.Out)) {
                    SetConsoleCtrlCHook(collector);

                    ManualResetEvent ev = new ManualResetEvent(false);
                    ThreadPool.QueueUserWorkItem((o) => {
                        collector.Start();

                        ev.Set();
                    });
                    ThreadPool.QueueUserWorkItem((o) => {
                        process.Join();
                        collector.Dispose();
                    });

                    Thread.Sleep(1000);

                    // resume thread
                    process.Resume();

                    ev.WaitOne();
                }
            }
        }

        static void TraceRunningProcess(int pid)
        {
            using (var collector = new TraceCollector(pid, Console.Out)) {
                SetConsoleCtrlCHook(collector);
                ThreadPool.QueueUserWorkItem((o) => {
                    var hProcess = WinProcesses.NativeMethods.OpenProcess(WinProcesses.ProcessAccessFlags.Synchronize, false, pid);
                    WinHandles.NativeMethods.WaitForSingleObject(hProcess, VsChromium.Core.Win32.Constants.INFINITE);

                    collector.Dispose();
                });

                collector.Start();
            }
        }

        static void SetConsoleCtrlCHook(TraceCollector collector)
        {
            // Set up Ctrl-C to stop both user mode and kernel mode sessions
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) => {
                collector.Dispose();
                cancelArgs.Cancel = true;
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
