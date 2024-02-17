
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public class AuthProviderService
    {
        private readonly string resourcePath = ".well-known/openid-configuration";
        private readonly AzureAuthOperationsConfig _config;
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        public AuthProviderService(IHttpClientFactory httpClientFactory, IMemoryCache cache, AzureAuthOperationsConfig config, ILogger<AuthProviderService> logger) 
        {
            _httpClient = httpClientFactory.CreateClient("FHIRClient");
            _cache = cache;
            _config = config;
            _logger = logger;
            _ = SetEndpoints();
        }

        
        public async Task SetEndpoints()
        {
            try
            {
                if(_cache.TryGetValue("Issuer", out var cachedIssuer) && _cache.TryGetValue("AuthorizationEndpoint", out var cachedAuthEndpoint) && _cache.TryGetValue("TokenEndpoint", out var cachedTokenEndpoint))
                {
                    _config.Issuer = cachedIssuer.ToString();
                    _config.Authorization_Endpoint = cachedAuthEndpoint.ToString();
                    _config.Token_Endpoint = cachedTokenEndpoint.ToString();
                    return;
                }

                var response = await _httpClient.GetAsync(resourcePath);
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                var jsonNode = JsonNode.Parse(jsonString);
                if (jsonNode != null)
                {
                    var issuer = jsonNode["issuer"]?.ToString();
                    _config.Issuer = issuer;
                    var authEndpoint = jsonNode["authorization_endpoint"]?.ToString();
                    _config.Authorization_Endpoint = authEndpoint;
                    var tokenEndpoint = jsonNode["token_endpoint"]?.ToString();
                    _config.Token_Endpoint = tokenEndpoint;

                    if (issuer == null || authEndpoint == null || tokenEndpoint == null)
                    {
                        _logger.LogError("The JSON does not contain the required endpoints.");
                        throw new JsonException("The JSON does not contain the required endpoints.");
                    }

                    _cache.Set("Issuer", issuer);
                    _cache.Set("AuthorizationEndpoint", authEndpoint);
                    _cache.Set("TokenEndpoint", tokenEndpoint);
                }
                else throw new JsonException("The JSON body cannot be null");

            }
            catch (HttpRequestException)
            {
                throw;
            } 
        }
    }
}
