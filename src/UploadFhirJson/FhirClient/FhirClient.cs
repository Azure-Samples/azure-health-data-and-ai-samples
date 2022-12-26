using Azure.Core;
using Azure.Identity;
using System.Text;

namespace UploadFhirJson.FhirClient
{
    public class FhirClient : IFhirClient
    {
        private readonly HttpClient _httpClient;

        public FhirClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            var accessToken = FetchToken();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken.Result}");
        }

        private async Task<string> FetchToken()
        {          
            string[] scopes = new string[] { $"{_httpClient.BaseAddress}/.default" };
            TokenRequestContext tokenRequestContext = new TokenRequestContext(scopes: scopes);
            var credential = new DefaultAzureCredential(true);
            var token = await credential.GetTokenAsync(tokenRequestContext);          
            return token.Token;
        }

        public async Task<HttpResponseMessage> Send(string reqBody)
        {
            var content = new StringContent(reqBody, Encoding.UTF8, "application/json");
            return await _httpClient.PostAsync("", content);
        }

    }
}
