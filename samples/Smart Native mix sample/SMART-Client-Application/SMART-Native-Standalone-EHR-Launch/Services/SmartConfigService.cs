using System.Collections.Concurrent;
using System.Text.Json;

public class SmartConfigService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmartConfigService> _logger;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    // Cache keyed by the normalised FHIR base URL so standalone and each EHR ISS
    // share a slot without interfering with each other.
    private readonly ConcurrentDictionary<string, (SmartConfiguration config, string rawJson, DateTimeOffset fetchedAt)> _cache = new();

    public SmartConfigService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SmartConfigService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Returns the SMART configuration for the given ISS URL.
    /// When <paramref name="issUrl"/> is null the configured FhirBaseUrl is used (standalone path).
    /// </summary>
    public async Task<SmartConfiguration> GetConfigurationAsync(CancellationToken cancellationToken, string? issUrl = null)
    {
        var baseUrl = NormaliseBaseUrl(issUrl);
        if (_cache.TryGetValue(baseUrl, out var cached) && !IsExpired(cached.fetchedAt))
            return cached.config;

        var (config, rawJson) = await FetchConfigurationAsync(baseUrl, cancellationToken);
        _cache[baseUrl] = (config, rawJson, DateTimeOffset.UtcNow);
        return config;
    }

    /// <summary>
    /// Returns the raw JSON SMART configuration for display purposes.
    /// </summary>
    public async Task<string> GetRawConfigurationJsonAsync(CancellationToken cancellationToken, string? issUrl = null)
    {
        var baseUrl = NormaliseBaseUrl(issUrl);
        if (_cache.TryGetValue(baseUrl, out var cached) && !IsExpired(cached.fetchedAt))
            return cached.rawJson;

        var (config, rawJson) = await FetchConfigurationAsync(baseUrl, cancellationToken);
        _cache[baseUrl] = (config, rawJson, DateTimeOffset.UtcNow);
        return rawJson;
    }

    private string NormaliseBaseUrl(string? issUrl)
    {
        var url = string.IsNullOrWhiteSpace(issUrl)
            ? _configuration["SmartOnFhir:FhirBaseUrl"]
            : issUrl;

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("SmartOnFhir:FhirBaseUrl is not configured.");

        return url.TrimEnd('/');
    }

    private async Task<(SmartConfiguration config, string rawJson)> FetchConfigurationAsync(
        string normalisedBase, CancellationToken cancellationToken)
    {
        var discoveryUrl = normalisedBase + "/.well-known/smart-configuration";
        _logger.LogInformation("Fetching SMART configuration from {Url}", discoveryUrl);

        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(discoveryUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to fetch SMART configuration. StatusCode={StatusCode}, Body={Body}", response.StatusCode, body);
            throw new HttpRequestException($"Failed to fetch SMART configuration: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var config = JsonSerializer.Deserialize<SmartConfiguration>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (config == null || string.IsNullOrWhiteSpace(config.AuthorizationEndpoint) || string.IsNullOrWhiteSpace(config.TokenEndpoint))
            throw new InvalidOperationException("SMART configuration is missing required endpoints.");

        return (config, json);
    }

    private bool IsExpired(DateTimeOffset fetchedAt) =>
        DateTimeOffset.UtcNow - fetchedAt > _cacheTtl;
}
