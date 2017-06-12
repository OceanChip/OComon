using OceanChip.Common.Components;
using OceanChip.Common.Configurations;
using OceanChip.Common.Logging;
using OceanChip.Common.Remoting;
using OceanChip.Common.Socketing;
using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OConfig = OceanChip.Common.Configurations.Configuration;

namespace RemoteingPerformanceTest.Client
{
    class Program
    {
        static string _performanceKey = "SendMessage";
        static string _mode;
        static int _messageCount;
        static byte[] _message;
        static ILogger _logger;
        static IPerformanceService _performanceService;
        static SocketRemotingClient _client;

        static void Main(string[] args)
        {
            InitializeOCommon();
            StartSendMessageTest();
            Console.ReadLine();
        }

        static void InitializeOCommon()
        {
            _message = new byte[int.Parse(ConfigurationManager.AppSettings["MessageSize"])];
            _mode = ConfigurationManager.AppSettings["Mode"];

            _messageCount = int.Parse(ConfigurationManager.AppSettings["MessageCount"]);

            var logContextText = "mode:" + _mode;

            OConfig.Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4net()
                .RegisterUnhandledExceptionHandler();

            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(Program).Name);
            _performanceService = ObjectContainer.Resolve<IPerformanceService>();
            var setting = new PerformanceServiceSetting
            {
                AutoLogging = false,
                StatIntervalSeconds = 1,
                PerformanceInfoHandler = x =>
                {
                    _logger.Info($"{_performanceService.Name},{logContextText},totalCount:{x.TotalCount}," +
                        $"throughput:{x.Throughput},averageThrughput:{x.AverageThroughput}," +
                        $"rt:{x.RT.ToString("F3")}ms,averageRT:{x.AverageRT.ToString("F3")}ms");
                }
            };
            _performanceService.Initialize(_performanceKey, setting);
            _performanceService.Start();
            

        }

        static void StartSendMessageTest()
        {
            var serverIP = ConfigurationManager.AppSettings["ServerAddress"];
            var serverAddress = string.IsNullOrEmpty(serverIP) ? SocketUtils.GetLocalIPV4() : IPAddress.Parse(serverIP);
            var sendAction = default(Action);

            _client = new SocketRemotingClient(new IPEndPoint(serverAddress, 5000)).Start();

            if (_mode == "OneWay")
            {
                sendAction = () =>
                {
                    var request = new RemotingRequest(100, _message);
                    _client.InvokeOnway(request);
                    _performanceService.IncrementKeyCount(_mode, (DateTime.Now - request.CreatedTime).TotalMilliseconds);
                };
            }
            else if (_mode == "Sync")
            {
                sendAction = () =>
                {
                    var request = new RemotingRequest(100, _message);
                    _client.InvokeSync(request, 5000);
                    _performanceService.IncrementKeyCount(_mode, (DateTime.Now - request.CreatedTime).TotalMilliseconds);
                };
            }
            else if (_mode == "Async")
            {
                sendAction = () =>
                {
                    var request = new RemotingRequest(100, _message);
                    _client.InvokeAsync(request, 100000).ContinueWith(t =>
                    {
                        if (t.Exception != null)
                        {
                            _logger.Error(t.Exception);
                            return;
                        }
                        var response = t.Result;
                        if (response.ResponseCode <= 0)
                        {
                            _logger.Error(Encoding.UTF8.GetString(response.ResponseBody));
                            return;
                        }
                        _performanceService.IncrementKeyCount(_mode, (DateTime.Now - request.CreatedTime).TotalMilliseconds);

                    });
                };
            }
            else if (_mode == "Callback")
            {
                _client.RegisterResponseHandler(100, new ResponseHandler(_performanceService, _mode));
                sendAction = () => _client.InvokeWithCallback(new RemotingRequest(100, _message));
            }

            Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < _messageCount; i++)
                {
                    try
                    {
                        sendAction();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex.Message, ex);
                        Thread.Sleep(3000);
                    }
                }
            });
        }
        class ResponseHandler : IResponseHandler
        {
            private IPerformanceService _performanceService;
            private string _performanceKey;

            public ResponseHandler(IPerformanceService performanceService, string performanceKey)
            {
                _performanceService = performanceService;
                _performanceKey = performanceKey;
            }

            public void HandleResponse(RemotingResponse remotingResponse)
            {
                if (remotingResponse.RequestCode <= 0)
                {
                    _logger.Error(Encoding.UTF8.GetString(remotingResponse.ResponseBody));
                    return;
                }
                _performanceService.IncrementKeyCount(_mode, (DateTime.Now - remotingResponse.RequestTime).TotalMilliseconds);

            }
        }
    }
}
