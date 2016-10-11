using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LowLevelDesign.WinTrace.Handlers
{
    class FileIOTraceEventHandler : ITraceEventHandler
    {
        private readonly TextWriter output;
        private readonly int pid;
        private readonly Dictionary<ulong, string> fileObjectToFileNameMap = new Dictionary<ulong, string>();

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

        private void HandleFileIOSimpleOpTraceData(FileIOSimpleOpTraceData data)
        {
            if (data.ProcessID == pid) {
                ulong fileObject = data.FileObject;
                string fileName;
                fileObjectToFileNameMap.TryGetValue(fileObject, out fileName);

                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{fileName}' (0x{fileObject:X})");
            }
        }

        private void HandleFileIOCreateTraceData(FileIOCreateTraceData data)
        {
            if (data.ProcessID == pid) {
                string fileName = data.FileName;
                ulong fileObject = data.FileObject;

                if (!fileObjectToFileNameMap.ContainsKey(fileObject)) {
                    fileObjectToFileNameMap.Add(fileObject, fileName);
                }

                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{fileName}' (0x{fileObject:X}) " +
                    GenerateFileShareMask(data.ShareAccess) + GenerateFileAttributeMask(data.FileAttributes));
            }
        }

        private void HandleFileIOInfoTraceData(FileIOInfoTraceData data)
        {
            if (data.ProcessID == pid) {
                ulong fileObject = data.FileObject;
                string fileName;
                fileObjectToFileNameMap.TryGetValue(fileObject, out fileName);

                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{fileName}' (0x{fileObject:X})");
            }
        }

        private void HandleFileIODirEnumTraceData(FileIODirEnumTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{data.FileName}' (0x{data.FileObject:X})");
            }
        }

        private void HandleFileIONameTraceData(FileIONameTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{data.FileName}' (0x{data.FileKey:X})");
            }
        }

        private void HandleFileIOOpEndTraceData(FileIOOpEndTraceData data)
        {
        }

        private void HandleFileIOReadWriteTraceData(FileIOReadWriteTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{data.FileName}' (0x{data.FileObject:X})" +
                    $" 0x{data.Offset:X} {data.IoSize}b");
            }
        }

        private string GenerateFileShareMask(FileShare share)
        {
            if ((share & FileShare.ReadWrite & FileShare.Delete) != 0) {
                return "rwd";
            }
            if ((share & FileShare.ReadWrite) != 0) {
                return "rw-";
            }
            if ((share & FileShare.Read) != 0) {
                return "-r-";
            }
            if ((share & FileShare.Write) != 0) {
                return "-w-";
            }
            return "---";
        }

        private string GenerateFileAttributeMask(FileAttributes attr)
        {
            var buffer = new StringBuilder();
            if ((attr & FileAttributes.Archive) != 0) {
                buffer.Append(":A");
            } 
            if ((attr & FileAttributes.Compressed) != 0) {
                buffer.Append(":C");
            }
            if ((attr & FileAttributes.Device) != 0) {
                buffer.Append(":DEV");
            }
            if ((attr & FileAttributes.Directory) != 0) {
                buffer.Append(":D");
            }
            if ((attr & FileAttributes.Encrypted) != 0) {
                buffer.Append(":ENC");
            }
            if ((attr & FileAttributes.Hidden) != 0) {
                buffer.Append(":H");
            }
            if ((attr & FileAttributes.IntegrityStream) != 0) {
                buffer.Append(":ISR");
            }
            if ((attr & FileAttributes.NoScrubData) != 0) {
                buffer.Append(":NSD");
            }
            if ((attr & FileAttributes.NotContentIndexed) != 0) {
                buffer.Append(":NCI");
            }
            if ((attr & FileAttributes.Offline) != 0) {
                buffer.Append(":OFF");
            }
            if ((attr & FileAttributes.ReadOnly) != 0) {
                buffer.Append(":RO");
            }
            if ((attr & FileAttributes.ReparsePoint) != 0) {
                buffer.Append(":RP");
            }
            if ((attr & FileAttributes.SparseFile) != 0) {
                buffer.Append(":SF");
            }
            if ((attr & FileAttributes.System) != 0) {
                buffer.Append(":SYS");
            }
            if ((attr & FileAttributes.Temporary) != 0) {
                buffer.Append(":TMP");
            }
            return buffer.ToString();
        }
    }
}
