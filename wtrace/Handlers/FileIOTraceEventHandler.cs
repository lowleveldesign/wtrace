using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;
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
            OperationEnd = 76,
            Read = 67,
            Write = 68,
            Cleanup = 65,
            Close = 66,
            Flush = 73,
            Create = 64
        }

        struct FileIoEvent
        {
            public string EventMessage;
            public ulong Irp;
            public double StartTimeRelativeInMS;
        }

        private readonly Dictionary<ulong, FileIoEvent> startedIoEvents = new Dictionary<ulong, FileIoEvent>();

        private static bool IsThisKnownOpCode(OpCodes id, TraceEvent data)
        {
            return (byte)id == (byte)data.Opcode;
        }

        private void StoreTraceEvent(FileIoEvent ev)
        {
            if (startedIoEvents.ContainsKey(ev.Irp)) {
                Console.WriteLine("## ERROR: IRP (0x{0:X}) reused.", ev.Irp);
                startedIoEvents.Remove(ev.Irp);
            }
            startedIoEvents.Add(ev.Irp, ev);
        }

        private FileIoEvent GenerateFileIoEvent(TraceEvent data, ulong irp, ulong fileKey, string fileName)
        {
            return new FileIoEvent {
                Irp = irp,
                EventMessage = $"{data.TimeStampRelativeMSec:0.0000} ({data.ThreadID}) {data.EventName}, file: '{fileName}' (0x{fileKey:X})",
                StartTimeRelativeInMS = data.TimeStampRelativeMSec
            };
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
            if (IsThisKnownOpCode(OpCodes.OperationEnd, data)) {
                var s = (FileIOOpEndTraceData)data;
                FileIoEvent startedEvent;
                if (startedIoEvents.TryGetValue(s.IrpPtr, out startedEvent)) {
                    var elapsed = s.TimeStampRelativeMSec - startedEvent.StartTimeRelativeInMS;
                    var result = $"{startedEvent.EventMessage}, elapsed: {elapsed:0.0000}ms";
                    startedIoEvents.Remove(s.IrpPtr);
                    return result;
                }

                // event not found - we will just print info
                return $"## ERROR: Failed to find an event for IRP: 0x{s.IrpPtr:X}";
            }
            if (IsThisKnownOpCode(OpCodes.Name, data) || IsThisKnownOpCode(OpCodes.FileCreate, data)
                || IsThisKnownOpCode(OpCodes.FileDelete, data) || IsThisKnownOpCode(OpCodes.FileRundown, data)) {
                var s = (FileIONameTraceData)data;
                return GenerateFileIoEvent(data, 0, s.FileKey, s.FileName).EventMessage;
            }

            if (IsThisKnownOpCode(OpCodes.Create, data)) {
                var s = (FileIOCreateTraceData)data;
                var ev = GenerateFileIoEvent(data, s.IrpPtr, s.FileObject, s.FileName);
                ev.EventMessage += ": " + GenerateFileShareMask(s.ShareAccess);
                StoreTraceEvent(ev);
            } else if (IsThisKnownOpCode(OpCodes.Cleanup, data) || IsThisKnownOpCode(OpCodes.Close, data) 
                || IsThisKnownOpCode(OpCodes.Flush, data)) {
                var s = (FileIOSimpleOpTraceData)data;
                StoreTraceEvent(GenerateFileIoEvent(data, s.IrpPtr, s.FileObject, s.FileName));
            } else if (IsThisKnownOpCode(OpCodes.Read, data) || IsThisKnownOpCode(OpCodes.Write, data)) {
                var s = (FileIOReadWriteTraceData)data;
                var ev = GenerateFileIoEvent(data, s.IrpPtr, s.FileObject, s.FileName);
                ev.EventMessage += string.Format(", offset: 0x{0:X}", s.Offset);
                StoreTraceEvent(ev);
            } else if (IsThisKnownOpCode(OpCodes.DirEnum, data) || IsThisKnownOpCode(OpCodes.DirNotify, data)) {
                var s = (FileIODirEnumTraceData)data;
                var ev = GenerateFileIoEvent(data, s.IrpPtr, s.FileObject, s.FileName);
                ev.EventMessage += string.Format(", directory: '{0}'", s.DirectoryName);
                StoreTraceEvent(ev);
            } else {
                Debug.Assert(IsThisKnownOpCode(OpCodes.SetInfo, data) || IsThisKnownOpCode(OpCodes.Delete, data)
                    || IsThisKnownOpCode(OpCodes.Rename, data) || IsThisKnownOpCode(OpCodes.QueryInfo, data)
                    || IsThisKnownOpCode(OpCodes.FSControl, data));
                var s = (FileIOInfoTraceData)data;
                StoreTraceEvent(GenerateFileIoEvent(data, s.IrpPtr, s.FileObject, s.FileName));
            }
            return string.Empty;
        }

        public bool ShouldHandle(TraceEvent data)
        {
            return Enum.IsDefined(typeof(OpCodes), (byte)data.Opcode);
        }
    }
}
