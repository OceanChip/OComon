using OceanChip.Common.Components;
using OceanChip.Common.Configurations;
using OceanChip.Common.Logging;
using OceanChip.Common.Remoting;
using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OConfig = OceanChip.Common.Configurations.Configuration;

namespace RemoteingPerformanceTest.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            OConfig.Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4net()
                .RegisterUnhandledExceptionHandler();

            new SocketRemotingServer().RegisterRequestHandler(100, new RequestHandler()).Start();
            Console.ReadLine();
        }
        class RequestHandler : IRequestHandler
        {
            private readonly ILogger _logger;
            private readonly string _performanceKey = "ReceiveMessage";
            private readonly IPerformanceService _performanceService;
            private readonly byte[] response = new byte[0];

            public RequestHandler()
            {
                _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
                _performanceService = ObjectContainer.Resolve<IPerformanceService>();
                var setting = new PerformanceServiceSetting
                {
                    AutoLogging = false,
                    StatIntervalSeconds = 1,
                    PerformanceInfoHandler = x =>
                    {
                        _logger.InfoFormat("{0}, totalCount: {1}, throughput: {2}, averageThrughput: {3}, rt: {4:F3}ms, averageRT: {5:F3}ms", _performanceService.Name, x.TotalCount, x.Throughput, x.AverageThroughput, x.RT, x.AverageRT);
                    }
                };
                _performanceService.Initialize(_performanceKey, setting);
                _performanceService.Start();
            }
            public RemotingResponse HandleRequest(IRequestHandlerContext context, RemotingRequest remotingRequest)
            {
                var currentTime = DateTime.Now;
                _performanceService.IncrementKeyCount(_performanceKey, (currentTime - remotingRequest.CreatedTime).TotalMilliseconds);
                return new RemotingResponse(
                    remotingRequest.Type,
                    remotingRequest.Code,
                    remotingRequest.Sequence,
                    remotingRequest.CreatedTime,
                    10,
                    response,
                    currentTime,
                    remotingRequest.Header,
                    null);
            }
        }
    }
}
