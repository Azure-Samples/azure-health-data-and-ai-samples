using Azure.Core;
using Azure.Identity;
using System.Text;
using UploadFhirJson.Caching;
using UploadFhirJson.Configuration;

namespace UploadFhirJson.FhirClient
{
    public class FhirClient : IFhirClient
    {
        private readonly IHttpClientFactory _httpClient;
        private readonly IAuthTokenCache _memoryCache;
        private readonly ServiceConfiguration _serviceConfiguration;
        private string cacheToken => "authToken";
        private readonly HttpClient client;


        public FhirClient(ServiceConfiguration serviceConfiguration, IHttpClientFactory httpClient, IAuthTokenCache memoryCache)
        {
            _httpClient = httpClient;
            _memoryCache = memoryCache;
            _serviceConfiguration = serviceConfiguration;
            client = _httpClient.CreateClient();
            client.BaseAddress = new Uri(_serviceConfiguration.FhirURL);
        }

        private async Task<string> FetchToken(string baseAddress)
        {
            string[] scopes = new string[] { $"{baseAddress}/.default" };
            TokenRequestContext tokenRequestContext = new(scopes: scopes);
            var credential = new DefaultAzureCredential(true);
            var token = await credential.GetTokenAsync(tokenRequestContext);
            await _memoryCache.AddToken(cacheToken, token.Token);
            return token.Token;
        }

        public async Task<HttpResponseMessage> Send(string reqBody)
        {   
            string cacheToken = await GetTokenfromCache();
            var accessToken = cacheToken ?? await FetchToken(client.BaseAddress.ToString());
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            var content = new StringContent(reqBody, Encoding.UTF8, "application/json");
            return await client.PostAsync("", content);
        }

        private async Task<string> GetTokenfromCache()
        {
            if (_memoryCache != null)
            {
                string token = await _memoryCache.GetToken(cacheToken);
                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }
            }
            return null;
        }

    }
}
