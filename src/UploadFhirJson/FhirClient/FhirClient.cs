using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Polly;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using UploadFhirJson.Caching;
using UploadFhirJson.Configuration;

namespace UploadFhirJson.FhirClient
{
    public class FhirClient : IFhirClient
    {
        private readonly IHttpClientFactory _httpClient;
        private readonly IAuthTokenCache _memoryCache;
        private readonly ILogger _logger;
        private readonly ServiceConfiguration _serviceConfiguration;
        private string cacheToken => "authToken";
        private readonly HttpClient client;
        private static readonly HttpStatusCode[] httpStatusCodesWorthRetrying = {
            HttpStatusCode.RequestTimeout, // 408
            HttpStatusCode.InternalServerError, // 500
            HttpStatusCode.BadGateway, // 502
            HttpStatusCode.ServiceUnavailable, // 503
            HttpStatusCode.GatewayTimeout, // 504
            HttpStatusCode.TooManyRequests //429
        };


        public FhirClient(ServiceConfiguration serviceConfiguration, IHttpClientFactory httpClient, IAuthTokenCache memoryCache, ILogger<FhirClient> logger)
        {
            _httpClient = httpClient;
            _memoryCache = memoryCache;
            _logger = logger;
            _serviceConfiguration = serviceConfiguration;
            client = _httpClient.CreateClient();
            client.BaseAddress = new Uri(_serviceConfiguration.FhirURL);
        }

        private async Task<string> FetchToken(string baseAddress)
        {
            _logger.LogInformation($"Getting token from {baseAddress}");
            string[] scopes = new string[] { $"{baseAddress}/.default" };
            TokenRequestContext tokenRequestContext = new(scopes: scopes);
            var credential = new DefaultAzureCredential(true);
            var token = await credential.GetTokenAsync(tokenRequestContext);
            await _memoryCache.AddToken(cacheToken, token.Token);
            _logger.LogInformation($"Got token and added in cache.");
            return token.Token;
        }

        public async Task<HttpResponseMessage> Send(string reqBody)
        {
            _logger.LogInformation("Started calling fhir server");
            string cacheToken = await GetTokenfromCache();
            var accessToken = cacheToken ?? await FetchToken(client.BaseAddress.ToString());
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            var content = new StringContent(reqBody, Encoding.UTF8, "application/json");

            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
                .WaitAndRetryAsync(3, retryAttempt =>
                   TimeSpan.FromMilliseconds(5000), (result, timeSpan, retryCount, context) =>
                   {
                       _logger.LogWarning($"FHIR Request failed on a retryable status...Waiting {timeSpan} before next retry. Attempt {retryCount}");
                   }
                );

            HttpResponseMessage _fhirResponse =
            await retryPolicy.ExecuteAsync(async () =>
            {
                return await client.PostAsync("", content);

            });
            
            return _fhirResponse;
        }

        private async Task<string> GetTokenfromCache()
        {
            if (_memoryCache != null)
            {
                string token = await _memoryCache.GetToken(cacheToken);
                if (!string.IsNullOrEmpty(token))
                {
                    _logger.LogInformation("Got token from cache");
                    return token;
                }
            }
            return null;
        }

    }
}
