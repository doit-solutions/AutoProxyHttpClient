using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AutoProxyHttpClient.Sample
{
    record GeoIpResponse
    {
        [JsonPropertyName("query")]
        public string IpAddress { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;
    }

    class GeoIpClient
    {
        private readonly HttpClient _proxyClient;
        private readonly HttpClient _nativeClient;

        public GeoIpClient(HttpClient proxyClient, IHttpClientFactory clientFactory)
        {
            _proxyClient = proxyClient;
            _nativeClient = clientFactory.CreateClient();
        }

        public async Task<GeoIpResponse?> GetProxyIpAsync(CancellationToken cancellationToken)
        {
            return JsonSerializer.Deserialize<GeoIpResponse>(await _proxyClient.GetStringAsync("http://ip-api.com/json/", cancellationToken));
        }

        public async Task<GeoIpResponse?> GetNativeIpAsync(CancellationToken cancellationToken)
        {
            return JsonSerializer.Deserialize<GeoIpResponse>(await _nativeClient.GetStringAsync("http://ip-api.com/json/", cancellationToken));
        }
    }
}