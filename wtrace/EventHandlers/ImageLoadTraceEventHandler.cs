using LowLevelDesign.WinTrace.Utilities;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace LowLevelDesign.WinTrace.Handlers
{
    class ImageLoadTraceEventHandler : ITraceEventHandler
    {
        private readonly ITraceOutput traceOutput;
        private readonly int pid;

        public ImageLoadTraceEventHandler(int pid, ITraceOutput traceOutput)
        {
            this.traceOutput = traceOutput;
            this.pid = pid;
        }

        public void PrintStatistics(double sessionEndTimeInMs)
        {
        }

        public void SubscribeToEvents(TraceEventParser parser)
        {
            var kernel = (KernelTraceEventParser)parser;
            kernel.ImageDCStart += HandleImageLoad;
            kernel.ImageLoad += HandleImageLoad;
            kernel.ImageUnload += HandleImageUnload;
        }

        private void HandleImageUnload(ImageLoadTraceData data)
        {
            if (data.ProcessID == 0) {
                // System process (contain driver dlls)
                DriverImageUtilities.RemoveImage(data.ImageBase);
                return;
            }

            if (data.ProcessID == pid) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID,
                    data.EventName, $"0x{data.ImageBase:X8} '{data.FileName}' ({data.ImageSize}b)");
            }
        }

        private void HandleImageLoad(ImageLoadTraceData data)
        {
            if (data.ProcessID == 0) {
                // System process (contain driver dlls)
                DriverImageUtilities.AddImage(new FileImageInMemory(data.FileName, data.ImageBase,
                    data.ImageSize));
                return;
            }

            if (data.ProcessID == pid) {
                traceOutput.Write(data.TimeStampRelativeMSec, data.ProcessID, data.ThreadID,
                    data.EventName, $"0x{data.ImageBase:X8} '{data.FileName}' ({data.ImageSize}b)");
            }
        }
    }
}
