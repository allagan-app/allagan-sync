using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace AllaganSync.Services;

public class AllaganApiClient : IDisposable
{
    private readonly IPluginLog log;
    private readonly ConfigurationService configService;
    private readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private const string ProductionBaseUrl = "https://allagan.app";

    public string BaseUrl
    {
        get
        {
#if DEBUG
            if (configService.DebugOverridesEnabled && !string.IsNullOrEmpty(configService.DebugBaseUrlOverride))
                return configService.DebugBaseUrlOverride.TrimEnd('/');
#endif
            return ProductionBaseUrl;
        }
    }

    public AllaganApiClient(IPluginLog log, ConfigurationService configService)
    {
        this.log = log;
        this.configService = configService;
    }

    public async Task<HttpResponseMessage> PostAsync(string endpoint, object payload, CancellationToken cancellationToken = default)
    {
        var token = ResolveToken();
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("No API token available.");

        var baseUrl = BaseUrl;
        var url = endpoint.StartsWith("/")
            ? $"{baseUrl}{endpoint}"
            : $"{baseUrl}/{endpoint}";

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

#if DEBUG
        log.Info($"POST {url}");
#endif

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> GetAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var baseUrl = BaseUrl;
        var url = endpoint.StartsWith("/")
            ? $"{baseUrl}{endpoint}"
            : $"{baseUrl}/{endpoint}";

#if DEBUG
        log.Info($"GET {url}");
#endif

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await httpClient.SendAsync(request, cancellationToken);
    }

    public bool HasToken()
    {
        return !string.IsNullOrEmpty(ResolveToken());
    }

    private string? ResolveToken()
    {
#if DEBUG
        if (configService.DebugOverridesEnabled && !string.IsNullOrEmpty(configService.DebugTokenOverride))
            return configService.DebugTokenOverride;
#endif

        return configService.CurrentCharacter?.ApiToken;
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
