using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Text;

namespace LowLevelDesign.WinTrace.EventHandlers
{
    class SystemConfigTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput output;
        private readonly List<string> buffer = new List<string>();

        public KernelTraceEventParser.Keywords RequiredKernelFlags => KernelTraceEventParser.Keywords.None;

        public SystemConfigTraceEventHandler(ITraceOutput output)
        {
            this.output = output;
        }

        public void SubscribeToSession(TraceEventSession session)
        {
            var kernel = session.Source.Kernel;
            kernel.SystemConfigCPU += Kernel_SystemConfigCPU;
            kernel.SystemConfigNIC += HandleConfigNIC;
            kernel.SystemConfigLogDisk += Kernel_SystemConfigLogDisk;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
            output.WriteSummary("System Configuration", String.Join(Environment.NewLine, buffer));
        }

        private void Kernel_SystemConfigCPU(SystemConfigCPUTraceData data)
        {
            buffer.Add($"Host: {data.ComputerName} ({data.DomainName})");
            buffer.Add($"CPU: {data.MHz}MHz {data.NumberOfProcessors}cores {data.MemSize}MB");
        }

        private void HandleConfigNIC(SystemConfigNICTraceData data)
        {
            buffer.Add($"NIC: {data.NICDescription} {data.IpAddresses}");
        }

        private void Kernel_SystemConfigLogDisk(SystemConfigLogDiskTraceData data)
        {
            long size = (data.BytesPerSector*data.SectorsPerCluster*data.TotalNumberOfClusters) >> 30;
            buffer.Add($"LOGICAL DISK: {data.DiskNumber} {data.DriveLetterString} {data.FileSystem} " +
                $"{size}GB");
        }
    }
}
