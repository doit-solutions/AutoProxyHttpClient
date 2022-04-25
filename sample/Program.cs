using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoProxyHttpClient.Sample
{
    class Program : IHostedService
    {
        static async Task Main(string[] args)
        {
            await Host
                .CreateDefaultBuilder(args)
                .ConfigureLogging((ctx, builder) => builder
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddDebug()
                    .AddConsole()
                )
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddHostedService<Program>()
                        .AddAutoProxyHttpClient<GeoIpClient>(AutoProxyHttpClientOptions.Create(apiKey: null, rotateProxies: true/*, PreferredGeographicProxyLocation.EU*/))
                            .ConfigureHttpClient(c => c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.114 Safari/537.36 Edg/91.0.864.59"));
                })
                .UseConsoleLifetime()
                .Build()
                .RunAsync();
        }

        private readonly IHostApplicationLifetime _lifetime;
        private readonly GeoIpClient _ipClient;

        private Task? _workTask;

        public Program(IHostApplicationLifetime lifetime, GeoIpClient ipClient)
        {
            _lifetime = lifetime;
            _ipClient = ipClient;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            #pragma warning disable 4014
            _workTask = Run(cancellationToken);
            #pragma warning restore 4014

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await (_workTask ?? Task.CompletedTask);
        }

        public async Task Run(CancellationToken cancellationToken)
        {
            var proxyIp1 = await _ipClient.GetProxyIpAsync(cancellationToken);
            var proxyIp2 = await _ipClient.GetProxyIpAsync(cancellationToken);
            var nativeIp = await _ipClient.GetNativeIpAsync(cancellationToken);

            System.Console.WriteLine($"Proxy 1 IP-address: {proxyIp1?.IpAddress ?? "Unknown"} (in {proxyIp1?.City ?? "Unknown"}, {proxyIp1?.Country ?? "Unknown"})");
            System.Console.WriteLine($"Proxy 2 IP-address: {proxyIp2?.IpAddress ?? "Unknown"} (in {proxyIp2?.City ?? "Unknown"}, {proxyIp2?.Country ?? "Unknown"})");
            System.Console.WriteLine($"Native IP-address: {nativeIp?.IpAddress ?? "Unknown"} (in {nativeIp?.City ?? "Unknown"}, {nativeIp?.Country ?? "Unknown"})");

            _lifetime.StopApplication();
        }
    }
}
