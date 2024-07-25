using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents.Services
{
    public class APIMService : IAPIMService
    {
        private readonly APIMDataConnection _connection;
        private readonly IHttpClientFactory _httpClient;
        private readonly IAccessTokenProvider _accessTokenProvider;
        public APIMService(APIMDataConnection aPIMDataConnection, IHttpClientFactory httpClient, IAccessTokenProvider accessTokenProvider)
        {
            _connection = aPIMDataConnection;
            _httpClient = httpClient;
            _accessTokenProvider = accessTokenProvider;
        }

        public async Task<HttpResponseMessage> GetPatientObservations(string firstName, string lastName)
        {
            if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
            {
                throw new ArgumentNullException("firstName&LastName");
            }

            HttpResponseMessage httpResponseMessage = null;
            try
            {
                //Observation?subject:Patient.given=firstName&subject:Patient.family=LastName
                if (!string.IsNullOrEmpty(lastName) && !string.IsNullOrEmpty(firstName))
                {
                    httpResponseMessage = await GetAsync($"Observation?subject:Patient.given={firstName}&subject:Patient.family={lastName}");
                }
                else if (string.IsNullOrEmpty(lastName))
                {
                    httpResponseMessage = await GetAsync($"Observation?subject:Patient.given={firstName}");
                }
                else if (string.IsNullOrEmpty(firstName))
                {
                    httpResponseMessage = await GetAsync($"observation?subject:Patient.family={lastName}");
                }

            }
            catch
            {
                throw;
            }

            return httpResponseMessage;
        }

        private async Task<HttpResponseMessage> GetAsync(string query)
        {
            HttpClient client;
            HttpResponseMessage _fhirResponse = new();
            
            try
            {
                string cacheToken = await FetchToken();

                Console.WriteLine("Token : " + cacheToken);

                client = _httpClient == null ? new HttpClient() : _httpClient.CreateClient();
                client.BaseAddress = new Uri(_connection.APIMUri);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cacheToken}");
                
                Console.WriteLine("Calling APIM " + _connection.APIMUri);

                var response = await client.GetAsync(query);
                Console.WriteLine("Response : " + response.Content);

                return response;

            }
            catch (Exception ex)
            {
                _fhirResponse.StatusCode = (HttpStatusCode)500;
                _fhirResponse.Content = new StringContent(ex.Message);
                Console.WriteLine(_fhirResponse.StatusCode.ToString() + " " + ex.Message);

                return _fhirResponse;
            }
        }

        private async Task<HttpResponseMessage> PutAsync(string uri, string content)
        {
            HttpClient client;
            HttpResponseMessage _fhirResponse = new();

            try
            {
                string cacheToken = await FetchToken();

                Console.WriteLine("Token : " + cacheToken);

                client = _httpClient == null ? new HttpClient() : _httpClient.CreateClient();
                client.BaseAddress = new Uri(_connection.APIMUri);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cacheToken}");

                Console.WriteLine("Calling APIM " + _connection.APIMUri);

                var response = await client.PutAsync(uri, new StringContent(content, System.Text.Encoding.UTF8, "application/json"));
                Console.WriteLine("Response : " + response.Content);

                return response;

            }
            catch (Exception ex)
            {
                _fhirResponse.StatusCode = (HttpStatusCode)500;
                _fhirResponse.Content = new StringContent(ex.Message);
                Console.WriteLine(_fhirResponse.StatusCode.ToString() + " " + ex.Message);

                return _fhirResponse;
            }
        }

        private async Task<HttpResponseMessage> PostAsync(string content)
        {
            HttpClient client;
            HttpResponseMessage _fhirResponse = new();

            try
            {
                string cacheToken = await FetchToken();

                Console.WriteLine("Token : " + cacheToken);

                client = _httpClient == null ? new HttpClient() : _httpClient.CreateClient();
                client.BaseAddress = new Uri(_connection.APIMUri);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cacheToken}");

                Console.WriteLine("Calling APIM " + _connection.APIMUri);

                var response = await client.PostAsync(string.Empty, new StringContent(content, System.Text.Encoding.UTF8, "application/json"));
                Console.WriteLine("Response : " + response.Content);

                return response;

            }
            catch (Exception ex)
            {
                _fhirResponse.StatusCode = (HttpStatusCode)500;
                _fhirResponse.Content = new StringContent(ex.Message);
                Console.WriteLine(_fhirResponse.StatusCode.ToString() + " " + ex.Message);

                return _fhirResponse;
            }
        }
         
        private async Task<string> FetchToken()
        {
            string accessToken = string.Empty;
            try
            {
                var accessTokenResult = await _accessTokenProvider.RequestAccessToken();

                if (accessTokenResult.TryGetToken(out var token))
                {
                    accessToken = token.Value;
                }

                return accessToken;
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public async Task<HttpResponseMessage> GetLookUpCode(string codeValue, string codeSystem)
        {
            if (string.IsNullOrEmpty(codeValue) || string.IsNullOrEmpty(codeValue))
            {
                throw new ArgumentNullException("codeValue");
            }
            HttpResponseMessage httpResponseMessage = null;
            try
            {
                if (!string.IsNullOrEmpty(codeValue))
                {
                    httpResponseMessage = await GetAsync($"CodeSystem/$lookup?system={codeSystem}&code={codeValue}");
                }
            }
            catch
            {
                throw;
            }

            return httpResponseMessage;

        }

        public async Task<HttpResponseMessage> TranslateCode(string sourceCode, string sourceCodeSystem, string targetCodeSystem)
        {
            if (string.IsNullOrEmpty(sourceCode) || string.IsNullOrEmpty(sourceCodeSystem) || string.IsNullOrEmpty(targetCodeSystem))
            {
                throw new ArgumentNullException("sourcecode&sourcecodeSystem&targetcodeSystem");
            }
            HttpResponseMessage httpResponseMessage;
            try
            {
                if (!string.IsNullOrEmpty(sourceCode) && !string.IsNullOrEmpty(sourceCodeSystem) && !string.IsNullOrEmpty(targetCodeSystem))
                {
                    httpResponseMessage = await GetAsync($"ConceptMap/$translate?code={sourceCode}&system={sourceCodeSystem}&targetsystem={targetCodeSystem}");
                }
                else
                {
                    httpResponseMessage = new HttpResponseMessage(HttpStatusCode.BadRequest);
                    httpResponseMessage.Content = new StringContent("Bad Request");
                }

            }
            catch
            {
                throw;
            }

            return httpResponseMessage;

        }

        public async Task<HttpResponseMessage> SaveObservation(string id, string observationJson)
        {
            HttpResponseMessage httpResponseMessage;
            try
            {
                httpResponseMessage = await PutAsync($"Observation/{id}", observationJson);
            }
            catch
            {
                throw;
            }
            return httpResponseMessage;
        }

        public async Task<HttpResponseMessage> ResetObservations(string observationBundle)
        {
            HttpResponseMessage httpResponseMessage;
            try
            {
                httpResponseMessage = await PostAsync(observationBundle);
            }
            catch
            {
                throw;
            }
            return httpResponseMessage;
        }


        public async Task<HttpResponseMessage> BatchOperationCall(string content)
        {
            HttpResponseMessage httpResponseMessage;
            try
            {
                string cacheToken = await FetchToken();
                if (cacheToken != "")
                {
                    httpResponseMessage = await PostAsync(content);
                }
                else
                {
                      httpResponseMessage = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    httpResponseMessage.Content = new StringContent("Unauthorized: Access is denied due to invalid credentials.");
                }
            }
            catch
            {
                throw;
            }
            return httpResponseMessage;
        }

    }
}
