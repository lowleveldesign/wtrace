using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System;

namespace LowLevelDesign.WinTrace.EventHandlers
{
    sealed class FileIOTraceEventHandler : ITraceEventHandler
    {
        class FileIoSummary
        {
            public long Read;

            public long Write;

            public long Total;
        }

        private readonly ITraceOutput traceOutput;
        private readonly int pid;
        private readonly Dictionary<ulong, string> fileObjectToFileNameMap = new Dictionary<ulong, string>();
        private readonly Dictionary<string, FileIoSummary> fileIoSummary = new Dictionary<string, FileIoSummary>();

        public FileIOTraceEventHandler(int pid, ITraceOutput output)
        {
            traceOutput = output;
            this.pid = pid;

        }

        public KernelTraceEventParser.Keywords RequiredKernelFlags => KernelTraceEventParser.Keywords.FileIOInit
                 | KernelTraceEventParser.Keywords.FileIO;

        public void SubscribeToSession(TraceEventSession session)
        {
            var kernel = session.Source.Kernel;
            kernel.FileIOClose += HandleFileIoSimpleOp;
            kernel.FileIOFlush += HandleFileIoSimpleOp;
            kernel.FileIOCreate += HandleFileIoCreate;
            kernel.FileIODelete += HandleFileIoInfo;
            kernel.FileIORename += HandleFileIoInfo;
            kernel.FileIOFileCreate += HandleFileIoName;
            kernel.FileIOFileDelete += HandleFileIoName;
            kernel.FileIOFileRundown += HandleFileIoName;
            kernel.FileIOName += HandleFileIoName;
            kernel.FileIORead += HandleFileIoReadWrite;
            kernel.FileIOWrite += HandleFileIoReadWrite;
            kernel.FileIOMapFile += HandleFileIoMapFile;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
            if (fileIoSummary.Count == 0) {
                return;
            }
            var buffer = new StringBuilder();
            foreach (var summary in fileIoSummary.OrderByDescending(kv => kv.Value.Total)) {
                if (buffer.Length != 0) {
                    buffer.AppendLine();
                }
                buffer.Append($"'{summary.Key}' W: {summary.Value.Write:0} b / R: {summary.Value.Read:0} b");
            }
            traceOutput.WriteSummary($"File I/O ({pid})", buffer.ToString());
        }

        private void HandleFileIoSimpleOp(FileIOSimpleOpTraceData data)
        {
            if (data.ProcessID == pid) {
                ulong fileObject = data.FileObject;
                string fileName;
                fileObjectToFileNameMap.TryGetValue(fileObject, out fileName);

                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, $"'{fileName}' (0x{fileObject:X})");
            }
        }

        private void HandleFileIoCreate(FileIOCreateTraceData data)
        {
            if (data.ProcessID == pid) {
                string fileName = data.FileName;
                ulong fileObject = data.FileObject;

                if (!fileObjectToFileNameMap.ContainsKey(fileObject)) {
                    fileObjectToFileNameMap.Add(fileObject, fileName);
                }

                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, $"'{fileName}' (0x{fileObject:X}) " +
                    GenerateFileShareMask(data.ShareAccess) + GenerateFileAttributeMask(data.FileAttributes));
            }
        }

        private void HandleFileIoInfo(FileIOInfoTraceData data)
        {
            if (data.ProcessID == pid) {
                ulong fileObject = data.FileObject;
                string fileName;
                fileObjectToFileNameMap.TryGetValue(fileObject, out fileName);

                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, $"'{fileName}' (0x{fileObject:X})");
            }
        }

        private void HandleFileIoName(FileIONameTraceData data)
        {
            if (data.ProcessID == pid) {
                string fileName = data.FileName;
                ulong fileObject = data.FileKey;

                if (!fileObjectToFileNameMap.ContainsKey(fileObject)) {
                    fileObjectToFileNameMap.Add(fileObject, fileName);
                }

                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, $"'{fileName}' (0x{data.FileKey:X})");
            }
        }

        private void HandleFileIoMapFile(MapFileTraceData data)
        {
            if (data.ProcessID == pid) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, $"'{data.FileName}' (0x{data.FileKey:X})");
            }
        }

        private void HandleFileIoReadWrite(FileIOReadWriteTraceData data)
        {
            if (data.ProcessID == pid) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID, data.EventName, $"'{data.FileName}' (0x{data.FileObject:X})" +
                    $" 0x{data.Offset:X} {data.IoSize}b");

                if (data.FileName != null) {
                    FileIoSummary summary;
                    if (!fileIoSummary.TryGetValue(data.FileName, out summary)) {
                        summary = new FileIoSummary();
                        fileIoSummary.Add(data.FileName, summary);
                    }
                    if ((byte)data.Opcode == 67) { // read
                        summary.Read += data.IoSize;
                        summary.Total += data.IoSize;
                    } else if ((byte)data.Opcode == 68) { // write
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
