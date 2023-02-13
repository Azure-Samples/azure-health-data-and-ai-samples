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

        public async Task<HttpResponseMessage> Send(string reqBody, string hl7FileName)
        {
            HttpResponseMessage _fhirResponse = new();
            try
            {
                string cacheToken = await GetTokenfromCache(); HttpClient client;
                client = _httpClient == null ? new HttpClient() : _httpClient.CreateClient();
                client.BaseAddress = new Uri(_serviceConfiguration.FhirURL); var accessToken = cacheToken ?? await FetchToken(client.BaseAddress.ToString());
                if (!client.DefaultRequestHeaders.Contains("Authorization"))
                {
                    Object lockobj = new(); lock (lockobj)
                    {
                        client.DefaultRequestHeaders.Clear();
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Remove("Authorization");
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    }
                }
                var content = new StringContent(reqBody, Encoding.UTF8, "application/json");
                var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
                .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromMilliseconds(15000), (result, timeSpan, retryCount, context) =>
                {
                    if (result != null && result.Result != null)
                    {
                        _logger.LogWarning($"hl7File:{hl7FileName} FHIR Request failed...Waiting {timeSpan} before next retry. Attempt {retryCount} with Status code: {(int)result.Result.StatusCode},at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                    }
                    else
                    {
                        _logger.LogWarning($"hl7File:{hl7FileName} FHIR Request failed...Waiting {timeSpan} before next retry. Attempt {retryCount}, at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                    }
                }
                );
                _logger.LogWarning($"hl7File: {hl7FileName} FHIR Request start at time: {DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}"); _fhirResponse =
                await retryPolicy.ExecuteAsync(async () =>
                {
                    var response = await client.PostAsync("", content);
                    _logger.LogWarning($"hl7File: {hl7FileName} FHIR Response with status code:{(int)response.StatusCode} at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                    return response;
                });
                _logger.LogWarning($"hl7File: {hl7FileName} FHIR Response end with status code:{_fhirResponse.StatusCode} at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while sending Fhir request to Converter for hl7file: {hl7FileName} at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                _fhirResponse.StatusCode = (HttpStatusCode)500;
                _fhirResponse.Content = new StringContent(ex.Message);
                return _fhirResponse;
            }
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
