using System;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Tracing;

namespace LowLevelDesign.WinTrace.Handlers
{
    class FileIOTraceEventHandler : ITraceEventHandler
    {
        class FileIoSummary
        {
            public long Read;

            public long Write;

            public long Total;
        }

        private readonly TextWriter output;
        private readonly int pid;
        private readonly Dictionary<ulong, string> fileObjectToFileNameMap = new Dictionary<ulong, string>();
        private readonly Dictionary<string, FileIoSummary> fileIoSummary = new Dictionary<string, FileIoSummary>();

        public FileIOTraceEventHandler(int pid, TextWriter output)
        {
            this.output = output;
            this.pid = pid;

        }

        public void SubscribeToEvents(KernelTraceEventParser kernel)
        {
            kernel.FileIOCleanup += HandleFileIOSimpleOp;
            kernel.FileIOClose += HandleFileIOSimpleOp;
            kernel.FileIOFlush += HandleFileIOSimpleOp;
            kernel.FileIOCreate += HandleFileIOCreate;
            kernel.FileIODelete += HandleFileIOInfo;
            kernel.FileIOFSControl += HandleFileIOInfo;
            kernel.FileIOQueryInfo += HandleFileIOInfo;
            kernel.FileIORename += HandleFileIOInfo;
            kernel.FileIOSetInfo += HandleFileIOInfo;
            kernel.FileIODirEnum += HandleFileIODirEnum;
            kernel.FileIODirNotify += HandleFileIODirEnum;
            kernel.FileIOFileCreate += HandleFileIOName;
            kernel.FileIOFileDelete += HandleFileIOName;
            kernel.FileIOFileRundown += HandleFileIOName;
            kernel.FileIOName += HandleFileIOName;
            kernel.FileIOOperationEnd += HandleFileIOOpEnd;
            kernel.FileIORead += HandleFileIOReadWrite;
            kernel.FileIOWrite += HandleFileIOReadWrite;
            kernel.FileIOMapFile += HandleFileIOMapFile;
        }

        public void PrintStatistics()
        {
            output.WriteLine("======= File I/O =======");
            output.WriteLine("File name - Writes / Reads");
            foreach (var summary in fileIoSummary.OrderByDescending(kv => kv.Value.Total)) {
                output.WriteLine($"{summary.Key} - {summary.Value.Write} / {summary.Value.Read}");
            }
        }

        private void HandleFileIOSimpleOp(FileIOSimpleOpTraceData data)
        {
            if (data.ProcessID == pid) {
                ulong fileObject = data.FileObject;
                string fileName;
                fileObjectToFileNameMap.TryGetValue(fileObject, out fileName);

                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{fileName}' (0x{fileObject:X})");
            }
        }

        private void HandleFileIOCreate(FileIOCreateTraceData data)
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

        private void HandleFileIOInfo(FileIOInfoTraceData data)
        {
            if (data.ProcessID == pid) {
                ulong fileObject = data.FileObject;
                string fileName;
                fileObjectToFileNameMap.TryGetValue(fileObject, out fileName);

                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{fileName}' (0x{fileObject:X})");
            }
        }

        private void HandleFileIODirEnum(FileIODirEnumTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{data.FileName}' (0x{data.FileObject:X})");
            }
        }

        private void HandleFileIOName(FileIONameTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{data.FileName}' (0x{data.FileKey:X})");
            }
        }

        private void HandleFileIOMapFile(MapFileTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{data.FileName}' (0x{data.FileKey:X})");
            }
        }

        private void HandleFileIOOpEnd(FileIOOpEndTraceData data)
        {
        }

        private void HandleFileIOReadWrite(FileIOReadWriteTraceData data)
        {
            if (data.ProcessID == pid) {
                output.WriteLine($"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName} '{data.FileName}' (0x{data.FileObject:X})" +
                    $" 0x{data.Offset:X} {data.IoSize}b");

                if (data.FileName != null) {
                    FileIoSummary summary;
                    if (!fileIoSummary.TryGetValue(data.FileName, out summary)) {
                        summary = new FileIoSummary();
                        fileIoSummary.Add(data.FileName, summary);
                    }
                    if ((uint) data.EventIndex == 67) { // read
                        summary.Read += data.IoSize;
                        summary.Total += data.IoSize;
                    } else if ((uint) data.EventIndex == 68) { // write
                        summary.Write += data.IoSize;
                        summary.Total += data.IoSize;
                    }
                }
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
