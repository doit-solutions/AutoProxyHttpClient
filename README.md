# AutoProxyHttpClient
A simple way to make HTTP requests using `System.Net.Http.HttpClient` from behind a proxy.

![NuGet](https://badgen.net/nuget/v/DoIt.AutoProxyHttpClient.json)

## Quick start
In order to use a proxied `HttpClient` instance instead of an ordinary instance, replace any calls to `AddHttpClient<TService>()` when configuring your application's services with `AddAutoProxyHttpClient<TService>`. It's as simple as that!

## How does it work?
Whenever an HTTP request is made using a proxied `HttpClient` instance, a list of available SOCKS5 proxies is fetched using [Proxyscrape](https://proxyscrape.com/home)'s API. Attempts to make the actual requested HTTP request is made with each of these proxies in turn until a successful resonse is received. The retry functionality is implemented using [Polly](https://github.com/App-vNext/Polly).

## What else?
It is possible to configure your proxied `HttpClient` to prefer proxies located in certain geographic regions. The default behavior is to use proxies from any geographic region. This is configured by passing one or more preferred geographc region when registrering the auto proxy services.

It is also possible to configure the proxied `HttpClient` instance to rotate the actual proxy it uses for each HTTP request. This might be helpful when attempting to avoid IP base rate limiting. The default behavior is to use a working proxy as long as it keeps working.

Finally, it is possible to apply any other configuration you see fit on the proxied `HttpClient` by calling `ConfigureHttpClient(Action<HttpClient>)`. This might, for example, be used to set default request headers.

```cs
void ConfigureServices(IServiceCollection services)
{
    // Register a service which will receive an HttpClient configured
    // to use proxies located in the EU or USA, to rotate which proxie
    // is used for each HTTP request and to send the request header
    // User-Agent with the value ProxiedService/1.0.0 with each request.
    services
        .AddAutoProxyHttpClient<MySuperServiceImplementation>(AutoProxyHttpClientOptions.Create(rotateProxies: true, PreferredGeographicProxyLocation.EU, PreferredGeographicProxyLocation.USA))
            .ConfigureHttpClient(c => c.DefaultRequestHeaders.Add("User-Agent", "ProxiedService/1.0.0"));
}
```
