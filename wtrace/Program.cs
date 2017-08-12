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
            bool showhelp = false, spawnNewConsoleWindow = false;
            bool printSummary = true;

            int pid = 0;

            var p = new OptionSet
            {
                { "newconsole", "Start the process in a new console window.", v => { spawnNewConsoleWindow = v != null; } },
                { "nosummary", "Prints only ETW events - no summary at the end.", v => {
                    if (v != null) {
                        printSummary = false;
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

            // for diagnostics information
            Trace.Listeners.Add(new ConsoleTraceListener());

            var processTraceRunner = new TraceProcess(new ConsoleTraceOutput(), printSummary);

            SetConsoleCtrlCHook(processTraceRunner);

            try {
                if (!int.TryParse(procargs[0], out pid)) {
                    processTraceRunner.TraceNewProcess(procargs, spawnNewConsoleWindow);
                }
                else {
                    processTraceRunner.TraceRunningProcess(pid);
                }
            }
            catch (COMException ex) {
                if ((uint) ex.HResult == 0x800700B7) {
                    Console.Error.WriteLine("ERROR: could not start the kernel logger - make sure it is not running.");
                }
            }
            catch (Win32Exception ex) {
                Console.Error.WriteLine(
                    $"ERROR: an error occurred while trying to start or open the process, hr: 0x{ex.HResult:X8}, " + 
                        $"code: 0x{ex.NativeErrorCode:X8} ({ex.Message})." );
            }
#if !DEBUG
            catch (Exception ex) {
                Console.Error.WriteLine($"ERROR: severe error happened when starting application: {ex.Message}");
            }
#endif
        }

        static void SetConsoleCtrlCHook(TraceProcess processTraceRunner)
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
