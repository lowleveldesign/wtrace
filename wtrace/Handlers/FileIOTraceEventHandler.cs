using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Diagnostics;
using System.IO;

namespace LowLevelDesign.WinTrace.Handlers
{
    class FileIOTraceEventHandler : ITraceEventHandler
    {
        enum OpCodes : byte
        {
            DirEnum = 72,
            DirNotify = 77,
            SetInfo = 69,
            Delete = 70,
            Rename = 71,
            QueryInfo = 74,
            FSControl = 75,
            Name = 0,
            FileCreate = 32,
            FileDelete = 35,
            FileRundown = 36,
            Read = 67,
            Write = 68,
            Cleanup = 65,
            Close = 66,
            Flush = 73,
            Create = 64
        }

        private static bool IsThisKnownOpCode(OpCodes id, TraceEvent data)
        {
            return (byte)id == (byte)data.Opcode;
        }

        private string GenerateFileEventMessage(TraceEvent data, ulong fileObject, string fileName)
        {
            return $"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName}, file: '{fileName}' (0x{fileObject:X})";
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

        public string Handle(TraceEvent data)
        {
            if (IsThisKnownOpCode(OpCodes.Name, data) || IsThisKnownOpCode(OpCodes.FileCreate, data)
                || IsThisKnownOpCode(OpCodes.FileDelete, data) || IsThisKnownOpCode(OpCodes.FileRundown, data)) {
                var s = (FileIONameTraceData) data;
                return GenerateFileEventMessage(data, s.FileKey, s.FileName);
            }

            if (IsThisKnownOpCode(OpCodes.Create, data)) {
                var s = (FileIOCreateTraceData) data;
                return GenerateFileEventMessage(data, s.FileObject, s.FileName) + ": " + GenerateFileShareMask(s.ShareAccess);
            }

            if (IsThisKnownOpCode(OpCodes.Cleanup, data) || IsThisKnownOpCode(OpCodes.Close, data)
                || IsThisKnownOpCode(OpCodes.Flush, data)) {
                var s = (FileIOSimpleOpTraceData) data;
                return GenerateFileEventMessage(data, s.FileObject, s.FileName);
            }

            if (IsThisKnownOpCode(OpCodes.Read, data) || IsThisKnownOpCode(OpCodes.Write, data)) {
                var s = (FileIOReadWriteTraceData) data;
                return GenerateFileEventMessage(data, s.FileObject, s.FileName) + $", offset: 0x{s.Offset:X}";
            }

            if (IsThisKnownOpCode(OpCodes.DirEnum, data) || IsThisKnownOpCode(OpCodes.DirNotify, data)) {
                var s = (FileIODirEnumTraceData) data;
                return GenerateFileEventMessage(data, s.FileObject, s.FileName) + $", directory: '{s.DirectoryName}'";
            }

            Debug.Assert(IsThisKnownOpCode(OpCodes.SetInfo, data) || IsThisKnownOpCode(OpCodes.Delete, data)
                         || IsThisKnownOpCode(OpCodes.Rename, data) || IsThisKnownOpCode(OpCodes.QueryInfo, data)
                         || IsThisKnownOpCode(OpCodes.FSControl, data));
            var d = (FileIOInfoTraceData) data;
            return GenerateFileEventMessage(data, d.FileObject, d.FileName);
        }

        public bool ShouldHandle(TraceEvent data)
        {
            return Enum.IsDefined(typeof(OpCodes), (byte)data.Opcode);
        }
    }
}
