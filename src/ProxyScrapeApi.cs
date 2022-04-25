using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public async Task<ICollection<ProxyScrapeProxy>> ListAvailableProxiesAsync(PreferredGeographicProxyLocation preferredLocation, string? apiKey, CancellationToken cancellationToken)
        {
            var resp = new List<ProxyScrapeProxy>();
            var countries = preferredLocation switch
            {
                PreferredGeographicProxyLocation.EU => new string[] { "at","be","bg","cy","cz","de","dk","ee","el","es","fi","fr","hr","hu","ie","it","lt","lu","lv","mt","nl","pl","pt","ro","se","si","sk" },
                PreferredGeographicProxyLocation.France => new string[] { "fr" },
                PreferredGeographicProxyLocation.Germany => new string[] { "de" },
                PreferredGeographicProxyLocation.Sweden => new string[] { "se" },
                PreferredGeographicProxyLocation.USA => new string[] { "us" },
                _ => new string[] { "all" }
            };
            var uri = string.IsNullOrWhiteSpace(apiKey) ? $"https://api.proxyscrape.com/v2/?request=getproxies&protocol=socks5&country={HttpUtility.UrlEncode(string.Join(",", countries))}" : $"https://api.proxyscrape.com/v2/account/datacenter_shared/proxy-list?auth={HttpUtility.UrlEncode(apiKey)}&type=getproxies&country[]={string.Join("&country[]=", countries.Select(c => HttpUtility.UrlEncode(c)))}&protocol=socks&format=normal&status=online";
            using (var stream = await _client.GetStreamAsync(uri))
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
