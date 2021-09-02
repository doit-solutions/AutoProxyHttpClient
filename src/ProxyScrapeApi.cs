using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace AutoProxyHttpClient
{
    record ProxyScrapeProxy(IPAddress Host, int Port);

    class ProxyScrapeApi
    {
        private static Regex ProxyLineRegex = new Regex(@"^(.+?):(\d+?)$");

        private readonly HttpClient _client;

        public ProxyScrapeApi(HttpClient client)
        {
            _client = client;
        }

        public async Task<ICollection<ProxyScrapeProxy>> ListAvailableProxiesAsync(PreferredGeographicProxyLocation preferredLocation, CancellationToken cancellationToken)
        {
            var resp = new List<ProxyScrapeProxy>();
            var countries = preferredLocation switch
            {
                PreferredGeographicProxyLocation.EU => "AT,BE,BG,CY,CZ,DE,DK,EE,EL,ES,FI,FR,HR,HU,IE,IT,LT,LU,LV,MT,NL,PL,PT,RO,SE,SI,SK",
                PreferredGeographicProxyLocation.France => "FR",
                PreferredGeographicProxyLocation.Germany => "DE",
                PreferredGeographicProxyLocation.Sweden => "SE",
                PreferredGeographicProxyLocation.USA => "US",
                _ => "all"
            };
            using (var stream = await _client.GetStreamAsync($"https://api.proxyscrape.com/?request=getproxies&proxytype=socks5&country={HttpUtility.UrlEncode(countries)}"))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var proxyLine = await reader.ReadLineAsync();
                    var match = ProxyLineRegex.Match(proxyLine ?? string.Empty);
                    if (match.Success && IPAddress.TryParse(match.Groups[1].Value, out IPAddress? ipAddress) && ipAddress != null && int.TryParse(match.Groups[2].Value, out int port))
                    {
                        resp.Add(new ProxyScrapeProxy(ipAddress, port));
                    }
                }
            }

            return resp;
        }
    }
}
