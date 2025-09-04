using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents.Services
{
	public class HttpHelperMethods : IHttpHelperMethods
	{
		private readonly IHttpClientFactory _httpClient;
		private readonly IAccessTokenProvider _accessTokenProvider;
		private readonly ILogger<HttpHelperMethods> logger;
		public HttpHelperMethods(IHttpClientFactory httpClient, IAccessTokenProvider accessTokenProvider, ILogger<HttpHelperMethods> logger)
        {
			_httpClient = httpClient;
			_accessTokenProvider = accessTokenProvider;
			this.logger = logger;
		}

        public async Task<HttpResponseMessage> PostAsync(string uri,string content, bool isFHIROperation)
		{
			HttpClient client;
			HttpResponseMessage _fhirResponse = new();

			try
			{
				client = _httpClient == null ? new HttpClient() : _httpClient.CreateClient();
				client.BaseAddress = new Uri(uri);
				client.DefaultRequestHeaders.Clear();
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Remove("Authorization");

				if(isFHIROperation)
				{
					string cacheToken = await FetchToken();
					client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cacheToken}");
				}

				logger.LogInformation("Calling PostAsync " + uri);

				var response = await client.PostAsync(string.Empty, new StringContent(content, System.Text.Encoding.UTF8, "application/json"));
				logger.LogInformation($"Response: {response.Content}");

				return response;

			}
			catch (Exception ex)
			{
				_fhirResponse.StatusCode = (HttpStatusCode)500;
				_fhirResponse.Content = new StringContent(ex.Message);
				logger.LogError(_fhirResponse.StatusCode.ToString() + " " + ex.Message);

				return _fhirResponse;
			}
		}
		public async Task<HttpResponseMessage> PutAsync(string uri, string content)
		{
			HttpClient client;
			HttpResponseMessage _fhirResponse = new();

			try
			{
				string cacheToken = await FetchToken();

				client = _httpClient == null ? new HttpClient() : _httpClient.CreateClient();
				client.BaseAddress = new Uri(uri);
				client.DefaultRequestHeaders.Clear();
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Remove("Authorization");
				client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cacheToken}");

				logger.LogInformation("Calling PutAsync " + uri);
				// Change to Patch
				var response = await client.PutAsync(uri, new StringContent(content, System.Text.Encoding.UTF8, "application/json"));
				logger.LogInformation($"Response: {response.Content}");

				return response;

			}
			catch (Exception ex)
			{
				_fhirResponse.StatusCode = (HttpStatusCode)500;
				_fhirResponse.Content = new StringContent(ex.Message);
				logger.LogInformation(_fhirResponse.StatusCode.ToString() + " " + ex.Message);
				return _fhirResponse;
			}
		}
		public async Task<HttpResponseMessage> DeleteAsync(string uri)
		{
			HttpClient client;
			HttpResponseMessage _fhirResponse = new();

			try
			{
				string cacheToken = await FetchToken();

				logger.LogInformation("Token : " + cacheToken);

				client = _httpClient == null ? new HttpClient() : _httpClient.CreateClient();
				client.BaseAddress = new Uri(uri);
				client.DefaultRequestHeaders.Clear();
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Remove("Authorization");
				client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cacheToken}");

				var response = await client.DeleteAsync(uri);
				logger.LogInformation("Response : " + response.Content);

				return response;
			}
			catch (Exception ex)
			{
				_fhirResponse.StatusCode = (HttpStatusCode)500;
				_fhirResponse.Content = new StringContent(ex.Message);
				logger.LogInformation(_fhirResponse.StatusCode.ToString() + " " + ex.Message);

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
	}
}
