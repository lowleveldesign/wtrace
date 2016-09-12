using Microsoft.Diagnostics.Tracing.Session;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Threading;
using WinHandles = VsChromium.Core.Win32.Handles;
using WinProcesses = VsChromium.Core.Win32.Processes;

namespace wtrace
{
    class Program
    {
        static void Main(string[] args)
        {
            if (TraceEventSession.IsElevated() != true) {
                Console.WriteLine("Must be elevated (Admin) to run this program.");
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
                Console.Write("ERROR: invalid argument");
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                showhelp = true;
            } catch (FormatException) {
                Console.WriteLine("ERROR: invalid number in one of the constraints");
                Console.WriteLine();
                showhelp = true;
            }

            if (!showhelp && (procargs.Count == 0 && pid == 0) || (pid > 0 && procargs.Count > 0)) {
                Console.WriteLine("ERROR: please provide either process name or PID of the already running process");
                Console.WriteLine();
                showhelp = true;
            }

            if (showhelp) {
                ShowHelp(p);
                return;
            }

            if (pid == 0) {
                TraceNewProcess(procargs);
            } else {
                TraceRunningProcess(pid);
            }
        }

        static void TraceNewProcess(IEnumerable<string> procargs)
        {
            using (var process= new ProcessCreator(procargs)) {
                process.StartSuspended();

                using (var collector = new TraceCollector("wtrace-session", process.ProcessId, Console.Out)) {
                    SetConsoleCtrlCHook(collector);

                    ManualResetEvent ev = new ManualResetEvent(false);
                    ThreadPool.QueueUserWorkItem((o) => {
                        collector.Start();

                        ev.Set();
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
            using (var collector = new TraceCollector("wtrace-session", pid, Console.Out)) {
                SetConsoleCtrlCHook(collector);
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
            Console.WriteLine("wtrace v{0} - allows you to set limits on your processes",
                Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Console.WriteLine("Copyright (C) 2016 Sebastian Solnica (@lowleveldesign)");
            Console.WriteLine();
            Console.WriteLine("Usage: wtrace [OPTIONS] args");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
        }

        class ProcessCreator : IDisposable
        {
            private readonly IEnumerable<string> args;
            private bool spawnNewConsoleWindow;
            private IntPtr hProcess;
            private IntPtr hThread;
            private int pid;

            public ProcessCreator(IEnumerable<string> args)
            {
                this.args = args;
            }

            public void StartSuspended()
            {
                var pi = new WinProcesses.PROCESS_INFORMATION();
                var si = new WinProcesses.STARTUPINFO();
                var processCreationFlags = WinProcesses.ProcessCreationFlags.CREATE_SUSPENDED;
                if (spawnNewConsoleWindow) {
                    processCreationFlags |= WinProcesses.ProcessCreationFlags.CREATE_NEW_CONSOLE;
                }

                if (!WinProcesses.NativeMethods.CreateProcess(null, new StringBuilder(string.Join(" ", args)), null, null, false,
                            processCreationFlags, null, null, si, pi)) {
                    throw new Win32Exception("Error while creating a new process.");
                }

                hProcess = pi.hProcess;
                pid = pi.dwProcessId;
                hThread = pi.hThread;
            }

            public void Resume()
            {
                if (WinProcesses.NativeMethods.ResumeThread(hThread) == -1) {
                    throw new Win32Exception("Error while resuming a process thread.");
                }
            }

            public int ProcessId
            {
                get { return pid; }
            }

            public bool SpawnNewConsoleWindow
            {
                get { return spawnNewConsoleWindow; }
                set { spawnNewConsoleWindow = value; }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (hThread != IntPtr.Zero) {
                    WinHandles.NativeMethods.CloseHandle(hThread);
                }
                if (hProcess != IntPtr.Zero) {
                    WinHandles.NativeMethods.CloseHandle(hProcess);
                }
            }

            ~ProcessCreator()
            {
                Dispose(false);
            }
        }
    }
}
