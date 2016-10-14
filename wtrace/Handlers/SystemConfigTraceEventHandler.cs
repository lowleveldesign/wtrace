using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.IO;

namespace LowLevelDesign.WinTrace.Handlers
{
    class SystemConfigTraceEventHandler : ITraceEventHandler
    {
        private readonly TextWriter output;

        public SystemConfigTraceEventHandler(TextWriter output)
        {
            this.output = output;
        }

        public void SubscribeToEvents(KernelTraceEventParser kernel)
        {
            kernel.SystemConfigCPU += Kernel_SystemConfigCPU;
            kernel.SystemConfigNIC += HandleConfigNIC;
            kernel.SystemConfigLogDisk += Kernel_SystemConfigLogDisk;
        }

        private void Kernel_SystemConfigCPU(SystemConfigCPUTraceData data)
        {
            output.WriteLine($"### CONFIG CPU: {data.ComputerName} {data.DomainName} {data.MHz}MHz " +
                $"{data.NumberOfProcessors}cores {data.MemSize}MB");
        }

        private void HandleConfigNIC(SystemConfigNICTraceData data)
        {
            output.WriteLine($"### CONFIG NIC: {data.NICDescription} {data.IpAddresses}");
        }

        private void Kernel_SystemConfigLogDisk(SystemConfigLogDiskTraceData data)
        {
            long size = (data.BytesPerSector*data.SectorsPerCluster*data.TotalNumberOfClusters) >> 30;
            output.WriteLine($"CONFIG LOGICAL DISK: {data.DiskNumber} {data.DriveLetterString} {data.FileSystem} " +
                $"{size}GB");
        }
    }
}
