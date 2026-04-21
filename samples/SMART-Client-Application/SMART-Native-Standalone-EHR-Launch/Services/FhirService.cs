using System.Net.Http.Headers;
using System.Text.Json;

public class FhirService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FhirService> _logger;

    public FhirService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<FhirService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Fetches a FHIR resource using the supplied access token.
    /// <paramref name="fhirBaseUrlOverride"/> is used for EHR launch (ISS from session).
    /// When null the configured <c>SmartOnFhir:FhirBaseUrl</c> is used (standalone path).
    /// </summary>
    public async Task<string> GetResourceAsync(
        string resourceType,
        string accessToken,
        CancellationToken cancellationToken,
        string? fhirBaseUrlOverride = null)
    {
        var fhirBaseUrl = fhirBaseUrlOverride;

        if (string.IsNullOrWhiteSpace(fhirBaseUrl))
        {
            fhirBaseUrl = _configuration["SmartOnFhir:FhirBaseUrl"];
            if (string.IsNullOrWhiteSpace(fhirBaseUrl))
                throw new InvalidOperationException("SmartOnFhir:FhirBaseUrl is not configured.");
        }

        if (!fhirBaseUrl.EndsWith("/", StringComparison.Ordinal))
            fhirBaseUrl += "/";

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(fhirBaseUrl);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync(resourceType, cancellationToken);
        var body     = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("FHIR {ResourceType} request failed. StatusCode={StatusCode}, Body={Body}", resourceType, response.StatusCode, body);
            throw new HttpRequestException($"FHIR {resourceType} request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        try
        {
            using var jsonDoc = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return body;
        }
    }
}
