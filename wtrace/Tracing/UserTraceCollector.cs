using LowLevelDesign.WinTrace.Handlers;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System.IO;

namespace LowLevelDesign.WinTrace.Tracing
{
    sealed class UserTraceCollector : TraceCollector
    {
        public UserTraceCollector(int pid, ITraceOutput output, TraceOutputOptions options)
            : base(CreateUserTraceEventSession())
        {
            TraceEventParser parser = new MicrosoftWindowsRPCTraceEventParser(traceSession.Source);
            ITraceEventHandler eventHandler = new RpcTraceEventHandler(pid, output, options);
            eventHandler.SubscribeToEvents(parser);
            eventHandlers.Add(eventHandler);

        }

        static TraceEventSession CreateUserTraceEventSession()
        {
            var userSession = new TraceEventSession("wtrace-customevents") {
                StopOnDispose = true
            };
            userSession.EnableProvider(MicrosoftWindowsRPCTraceEventParser.ProviderGuid, TraceEventLevel.Informational);

            return userSession;
        }
    }
}
