using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public class AuthProvider : IAuthProvider
    {
        private readonly HttpClient _httpClient;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly IMemoryCache _memoryCache;

        public AuthProvider(HttpClient httpClient, AzureAuthOperationsConfig configuration, IMemoryCache memoryCache)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _memoryCache = memoryCache;
        }

        public async Task<OpenIdConfiguration> GetOpenIdConfigurationAsync(string authorityUrl)
        {           
            // Try to get the OpenID configuration from cache
            if (_memoryCache.TryGetValue(authorityUrl, out OpenIdConfiguration? cachedConfig))
            {
                return cachedConfig!;
            }

            try
            {
                var openIdConfigurationUrl = $"{authorityUrl.TrimEnd('/')}/.well-known/openid-configuration";
                var response = await _httpClient.GetStringAsync(openIdConfigurationUrl);

                // Deserialize the JSON response
                var config = JsonConvert.DeserializeObject<OpenIdConfiguration>(response);

                // Cache the OpenID configuration for a specified duration 
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                };

                _memoryCache.Set(authorityUrl, config, cacheEntryOptions);

                return config!;
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching OpenID configuration.", ex);
            }
        }
    }
}
