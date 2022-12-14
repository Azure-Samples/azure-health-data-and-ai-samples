using Azure.Core;
using Azure.Identity;
using System.Text;
using UploadFhirJson.Caching;

namespace UploadFhirJson.FhirClient
{
    public class FhirClient : IFhirClient
    {
        private readonly HttpClient _httpClient;
        private readonly IInMemoryCache _inMemoryCache;

        public FhirClient(HttpClient httpClient, IInMemoryCache inMemoryCache)
        {
            _inMemoryCache = inMemoryCache;
            _httpClient = httpClient;
            var accessToken = FetchToken();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken.Result}");
        }


        private async Task<string> FetchToken()
        {
            //string cacheToken = await _inMemoryCache.GetAsync("token");
            //if (string.IsNullOrEmpty(cacheToken))
            //{
            string[] scopes = new string[] { $"{_httpClient.BaseAddress}/.default" };
            TokenRequestContext tokenRequestContext = new TokenRequestContext(scopes: scopes);
            var credential = new DefaultAzureCredential(true);
            var token = credential.GetToken(tokenRequestContext);
           // await _inMemoryCache.AddAsync("token", token.Token);
            return token.Token;
            //}
            //return cacheToken;
        }
        public async Task<HttpResponseMessage> Send(string reqBody)
        {
            var content = new StringContent(reqBody, Encoding.UTF8, "application/json");
            return await _httpClient.PostAsync("", content);

        }

    }
}
