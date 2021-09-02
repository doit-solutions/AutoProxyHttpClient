using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AutoProxyHttpClient;
using Polly;
using Polly.Extensions.Http;
using SocksSharp;
using SocksSharp.Proxy;

namespace Microsoft.Extensions.DependencyInjection
{
    public record AutoProxyHttpClientOptions
    {
        public static AutoProxyHttpClientOptions Default = Create(false);

        public static AutoProxyHttpClientOptions Create(bool rotateProxies, params PreferredGeographicProxyLocation[] preferredLocations)
        {
            return new AutoProxyHttpClientOptions
            {
                RotateProxies = rotateProxies,
                PreferredLocations = preferredLocations
            };
        }

        public bool RotateProxies { get; set; } = false;
        public PreferredGeographicProxyLocation[] PreferredLocations { get; set; } = new PreferredGeographicProxyLocation[] { PreferredGeographicProxyLocation.None };
    }

    public static class AutoProxyHttpClientServiceCollectionExtensions
    {
        private class PrioritizedProxyMessageHandler : DelegatingHandler
        {
            private static readonly PreferredGeographicProxyLocation[] PreferredGeographicProxyLocationNone = new PreferredGeographicProxyLocation[] { PreferredGeographicProxyLocation.None };

            private readonly ProxyScrapeApi _proxyApi;
            private readonly bool _rotateProxies;
            private readonly PreferredGeographicProxyLocation[] _preferredLocations;

            private ICollection<ProxyScrapeProxy> _availableProxies = Array.Empty<ProxyScrapeProxy>();

            public PrioritizedProxyMessageHandler(ProxyScrapeApi proxyApi, bool rotateProxies, params PreferredGeographicProxyLocation[] preferredLocations)
            {
                _proxyApi = proxyApi;
                _rotateProxies = rotateProxies;
                _preferredLocations = preferredLocations?.Any() ?? false ? preferredLocations : PreferredGeographicProxyLocationNone;

                InnerHandler = new ProxyClientHandler<Socks5>(new ProxySettings {});
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (!_availableProxies.Any())
                {
                    foreach (var preferredLocation in _preferredLocations)
                    {
                        _availableProxies = await _proxyApi.ListAvailableProxiesAsync(preferredLocation, cancellationToken);
                        if (_availableProxies.Any())
                        {
                            break;
                        }
                    }

                }
                
                while (_availableProxies?.Any() ?? false)
                {
                    var proxy = _availableProxies.First();
                    if (_rotateProxies)
                    {
                        _availableProxies = _availableProxies.Skip(1).Concat(_availableProxies.Take(1).ToList()).ToList();
                    }
                    try
                    {
                        if (InnerHandler is ProxyClientHandler<Socks5> ih)
                        {
                            ih.Proxy.Settings = new ProxySettings { Host = proxy.Host.ToString(), Port = proxy.Port };
                        }
                        return await base.SendAsync(request, cancellationToken);
                    }
                    catch
                    {
                        // Remove the bad proxy and try again (as long as we have other proxies to try).
                        _availableProxies = _availableProxies.Where(p => !p.Host.Equals(proxy.Host) || p.Port != proxy.Port).ToList();
                    }
                }

                throw new NoProxyAvailableException();
            }
        }

        public static IHttpClientBuilder AddAutoProxyHttpClient<TClient>(this IServiceCollection services, AutoProxyHttpClientOptions? options = null)
            where TClient : class
        {
            AddBaseProxyServices(services);
            return services
                .AddHttpClient<TClient>()
                    .ConfigureProxyClient(options ?? AutoProxyHttpClientOptions.Default);
        }

        public static IHttpClientBuilder AddAutoProxyHttpClient<TClient, TImplementation>(this IServiceCollection services, AutoProxyHttpClientOptions? options = null)
            where TClient : class
            where TImplementation : class, TClient
        {
            AddBaseProxyServices(services);
            return services
                .AddHttpClient<TClient, TImplementation>()
                    .ConfigureProxyClient(options ?? AutoProxyHttpClientOptions.Default);
        }

        private static void AddBaseProxyServices(IServiceCollection services)
        {
            var apiPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(attempt * attempt));
            services
                .AddHttpClient<ProxyScrapeApi>()
                    .ConfigureHttpClient(c => c.DefaultRequestHeaders.Add("User-Agent", "AutoProxyHttpClient"))
                    .AddPolicyHandler(apiPolicy);
        }

        private static IHttpClientBuilder ConfigureProxyClient(this IHttpClientBuilder builder, AutoProxyHttpClientOptions options)
        {
            return builder
                .ConfigurePrimaryHttpMessageHandler(services => new PrioritizedProxyMessageHandler(services.GetRequiredService<ProxyScrapeApi>(), options.RotateProxies, options.PreferredLocations))
                .AddPolicyHandler(CreateProxyPolicy());
        }

        private static AsyncPolicy<HttpResponseMessage> CreateProxyPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                // If we run out pf proxies, make sure we fetch a new list and try again.
                .Or<NoProxyAvailableException>()
                .WaitAndRetryAsync(1, attempt => TimeSpan.FromSeconds(attempt));
        }
    }
}
