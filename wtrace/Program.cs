using Microsoft.Diagnostics.Tracing.Session;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Utilities;

namespace LowLevelDesign.WinTrace
{
    static class Program
    {
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
            bool showhelp = false, spawnNewConsoleWindow = false,
                collectSystemStats = false, printSummary = true, traceChildProcesses = false;
            string eventNameFilter = null;

            int pid = 0;

            var p = new OptionSet
            {
                { "f|filter=", "Display only events which names contain the given keyword " +
                    "(case insensitive). Does not impact the summary.", v => { eventNameFilter = v; } },
                { "s|system", "Collect system statistics (DPC/ISR) - shown in the summary.", v => { collectSystemStats = v != null; } },
                { "c|children", "Trace process and all its children.", v => { traceChildProcesses = v != null; } },
                { "newconsole", "Start the process in a new console window.", v => { spawnNewConsoleWindow = v != null; } },
                { "nosummary", "Prints only ETW events - no summary at the end.", v => { printSummary = v == null; } },
                { "h|help", "Show this message and exit.", v => showhelp = v != null },
                { "?", "Show this message and exit.", v => showhelp = v != null }
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
            if (!showhelp && !collectSystemStats && procargs.Count == 0) {
                Console.Error.WriteLine("ERROR: please provide either process name, PID, or turn on system tracing (-s)");
                Console.Error.WriteLine();
                showhelp = true;
            }

            if (showhelp) {
                ShowHelp(p);
                return;
            }

            // for diagnostics information
#if DEBUG
            Trace.Listeners.Add(new ConsoleTraceListener());
#endif

            var traceSession = new TraceSession(new ConsoleTraceOutput(eventNameFilter), printSummary);

            SetConsoleCtrlCHook(traceSession);

            try {
                if (procargs.Count == 0) {
                    Console.WriteLine("System tracing has started. Press Ctrl + C to stop...");
                    traceSession.TraceSystemOnly();
                } else if (!int.TryParse(procargs[0], out pid)) {
                    traceSession.TraceNewProcess(procargs, spawnNewConsoleWindow, traceChildProcesses, 
                        collectSystemStats);
                } else {
                    traceSession.TraceRunningProcess(pid, traceChildProcesses, collectSystemStats);
                }
            } catch (COMException ex) {
                if ((uint)ex.HResult == 0x800700B7) {
                    Console.Error.WriteLine("ERROR: could not start the kernel logger - make sure it is not running.");
                }
            } catch (Win32Exception ex) {
                Console.Error.WriteLine(
                    $"ERROR: an error occurred while trying to start or open the process, hr: 0x{ex.HResult:X8}, " +
                        $"code: 0x{ex.NativeErrorCode:X8} ({ex.Message}).");
            }
#if !DEBUG
            catch (Exception ex) {
                Console.Error.WriteLine($"ERROR: severe error happened when starting application: {ex.Message}");
            }
#endif
        }

        static void SetConsoleCtrlCHook(TraceSession processTraceRunner)
        {
            // Set up Ctrl-C to stop both user mode and kernel mode sessions
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) => {
                cancelArgs.Cancel = true;
                processTraceRunner.Stop();
            };
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("wtrace v{0} - collects traces of Windows processes",
                Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Console.WriteLine("Copyright (C) 2017 Sebastian Solnica (@lowleveldesign)");
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
