using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using WinHandles = VsChromium.Core.Win32.Handles;
using WinProcesses = VsChromium.Core.Win32.Processes;

namespace LowLevelDesign.WinTrace
{
    class ProcessCreator : IDisposable
    {
        private readonly IEnumerable<string> args;
        private WinProcesses.SafeProcessHandle hProcess;
        private WinProcesses.SafeThreadHandle hThread;
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
            if (SpawnNewConsoleWindow) {
                processCreationFlags |= WinProcesses.ProcessCreationFlags.CREATE_NEW_CONSOLE;
            }

            if (!WinProcesses.NativeMethods.CreateProcess(null, new StringBuilder(string.Join(" ", args)), null, null, false,
                        processCreationFlags, null, null, si, pi)) {
                throw new Win32Exception("Error while creating a new process.");
            }

            hProcess = new WinProcesses.SafeProcessHandle(pi.hProcess);
            pid = pi.dwProcessId;
            hThread = new WinProcesses.SafeThreadHandle(pi.hThread);
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

        public bool SpawnNewConsoleWindow { get; set; }

        public void Join()
        {
            if (hProcess.IsInvalid || hProcess.IsClosed) {
                throw new InvalidOperationException();
            }
            WinHandles.NativeMethods.WaitForSingleObject(hProcess, VsChromium.Core.Win32.Constants.INFINITE);
        }

        public void Dispose()
        {
            hThread.Dispose();
            hProcess.Dispose();
        }
    }
}
