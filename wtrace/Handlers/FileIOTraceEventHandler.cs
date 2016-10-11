using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.IO;

namespace LowLevelDesign.WinTrace.Handlers
{
    class FileIOTraceEventHandler : ITraceEventHandler
    {
        private readonly TextWriter output;
        private readonly int pid;

        public FileIOTraceEventHandler(int pid, TextWriter output)
        {
            this.output = output;
            this.pid = pid;

        }

        public void SubscribeToEvents(KernelTraceEventParser kernel)
        {
            kernel.FileIOCleanup += HandleFileIOSimpleOpTraceData;
            kernel.FileIOClose += HandleFileIOSimpleOpTraceData;
            kernel.FileIOFlush += HandleFileIOSimpleOpTraceData;
            kernel.FileIOCreate += HandleFileIOCreateTraceData;
            kernel.FileIODelete += HandleFileIOInfoTraceData;
            kernel.FileIOFSControl += HandleFileIOInfoTraceData;
            kernel.FileIOQueryInfo += HandleFileIOInfoTraceData;
            kernel.FileIORename += HandleFileIOInfoTraceData;
            kernel.FileIOSetInfo += HandleFileIOInfoTraceData;
            kernel.FileIODirEnum += HandleFileIODirEnumTraceData;
            kernel.FileIODirNotify += HandleFileIODirEnumTraceData;
            kernel.FileIOFileCreate += HandleFileIONameTraceData;
            kernel.FileIOFileDelete += HandleFileIONameTraceData;
            kernel.FileIOFileRundown += HandleFileIONameTraceData;
            kernel.FileIOName += HandleFileIONameTraceData;
            kernel.FileIOOperationEnd += HandleFileIOOpEndTraceData;
            kernel.FileIORead += HandleFileIOReadWriteTraceData;
            kernel.FileIOWrite += HandleFileIOReadWriteTraceData;
        }

        private string GenerateFileShareMask(FileShare share)
        {
            if ((share & FileShare.ReadWrite & FileShare.Delete) != 0) {
                return "rwd";
            }
            if ((share & FileShare.ReadWrite) != 0) {
                return "rw";
            }
            if ((share & FileShare.Read) != 0) {
                return "r";
            }
            if ((share & FileShare.Write) != 0) {
                return "w";
            }
            return "none";
        }

        private void HandleFileIOSimpleOpTraceData(FileIOSimpleOpTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName}, file: '{data.FileName}' (0x{data.FileObject:X})");
            }
        }

        private void HandleFileIOCreateTraceData(FileIOCreateTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName}, file: '{data.FileName}' (0x{data.FileObject:X}), access: " +
                    GenerateFileShareMask(data.ShareAccess));
            }
        }

        private void HandleFileIOInfoTraceData(FileIOInfoTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName}, file: '{data.FileName}' (0x{data.FileObject:X})");
            }
        }

        private void HandleFileIODirEnumTraceData(FileIODirEnumTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName}, file: '{data.FileName}' (0x{data.FileObject:X})" +
                    $", directory: {data.DirectoryName}");
            }
        }

        private void HandleFileIONameTraceData(FileIONameTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName}, file: '{data.FileName}' (0x{data.FileKey:X})");
            }
        }

        private void HandleFileIOOpEndTraceData(FileIOOpEndTraceData data)
        {
        }

        private void HandleFileIOReadWriteTraceData(FileIOReadWriteTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName}, file: '{data.FileName}' (0x{data.FileObject:X})" +
                    $", off: 0x{data.Offset:X}, len: {data.IoSize}");
            }
        }
    }
}
