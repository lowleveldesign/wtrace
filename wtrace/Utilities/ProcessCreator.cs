using PInvoke;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LowLevelDesign.WinTrace.Utilities
{
    class ProcessCreator : IDisposable
    {
        private readonly IEnumerable<string> args;
        private Kernel32.SafeObjectHandle hProcess;
        private Kernel32.SafeObjectHandle hThread;
        private int pid;

        public ProcessCreator(IEnumerable<string> args)
        {
            this.args = args;
        }

        public void StartSuspended()
        {
            var pi = new Kernel32.PROCESS_INFORMATION();
            var si = new Kernel32.STARTUPINFO() {
                hStdInput = Kernel32.SafeObjectHandle.Null,
                hStdOutput = Kernel32.SafeObjectHandle.Null,
                hStdError = Kernel32.SafeObjectHandle.Null 
            };
            var processCreationFlags = Kernel32.CreateProcessFlags.CREATE_SUSPENDED | 
                                       Kernel32.CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT;
            if (SpawnNewConsoleWindow) {
                processCreationFlags |= Kernel32.CreateProcessFlags.CREATE_NEW_CONSOLE;
            }

            if (!Kernel32.CreateProcess(null, new StringBuilder(string.Join(" ", args)).ToString(), IntPtr.Zero, IntPtr.Zero, false,
                        processCreationFlags, IntPtr.Zero, null, ref si, out pi)) {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            hProcess = new Kernel32.SafeObjectHandle(pi.hProcess);
            pid = pi.dwProcessId;
            hThread = new Kernel32.SafeObjectHandle(pi.hThread);
        }

        public void Resume()
        {
            if (Kernel32.ResumeThread(hThread) == -1) {
                throw new System.ComponentModel.Win32Exception("Error while resuming a process thread.");
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
            Kernel32.WaitForSingleObject(hProcess, Constants.INFINITE);
        }

        public void Dispose()
        {
            if (hThread != null) {
                hThread.Dispose();
            }
            if (hProcess != null) {
                hProcess.Dispose();
            }
        }
    }
}
