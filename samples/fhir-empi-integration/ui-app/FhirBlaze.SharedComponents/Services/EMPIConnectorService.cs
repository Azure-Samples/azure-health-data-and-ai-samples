using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents.Services
{
	public class EMPIConnectorService : IEMPIConnectorService
	{
		private readonly EMPIConnectorConnection _eMPIConnection;
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<EMPIConnectorService> _logger;
		public EMPIConnectorService(EMPIConnectorConnection eMPIConnectorConnection, 
			IHttpClientFactory httpClientFactory, ILogger<EMPIConnectorService> logger)
		{
			_eMPIConnection = eMPIConnectorConnection;
			_httpClientFactory = httpClientFactory;
			_logger = logger;
		}
		public async Task<HttpResponseMessage> GetPatientMatchAsync(string requestBody)
		{
			HttpResponseMessage httpResponseMessage = null;
			try
			{
				if (!string.IsNullOrEmpty(requestBody))
				{
					_logger.LogInformation($"Calling PostAsync");
					//add code as a querystring with below url. currently it is removed as it does not allow to push the code to repo with this secret.
					httpResponseMessage = await PostAsync(@"api/Match", requestBody);
					_logger.LogError($"Response : {httpResponseMessage.Content}");
				}
			}
			catch (Exception ex)
			{
				httpResponseMessage.StatusCode = (HttpStatusCode)500;
				httpResponseMessage.Content = new StringContent(ex.Message);
				_logger.LogError(httpResponseMessage.StatusCode.ToString() + " " + ex.Message);
				throw;
			}

			return httpResponseMessage;
		}
		private async Task<HttpResponseMessage> PostAsync(string uri, string content)
		{
			HttpResponseMessage _empiConnectorResponse = new();

			try
			{
				HttpClient httpClient = _httpClientFactory.CreateClient();
				httpClient.BaseAddress = new Uri(_eMPIConnection.EMPIConnectorUri);
				httpClient.DefaultRequestHeaders.Clear();
				httpClient.DefaultRequestHeaders.Accept.Clear();
				httpClient.DefaultRequestHeaders.Remove("Authorization");

				_logger.LogInformation("Calling empi $match... " + _eMPIConnection.EMPIConnectorUri + uri);
				Console.WriteLine("Calling empi $match... " + _eMPIConnection.EMPIConnectorUri + uri);

				var response = await httpClient.PostAsync(uri, new StringContent(content, System.Text.Encoding.UTF8, "application/json"));
				_logger.LogInformation("Response : " + response.Content);
				Console.WriteLine("Response : " + response.Content);

				return response;

			}
			catch (Exception ex)
			{
				_empiConnectorResponse.StatusCode = (HttpStatusCode)500;
				_empiConnectorResponse.Content = new StringContent(ex.Message);
				_logger.LogError(_empiConnectorResponse.StatusCode.ToString() + " " + ex.Message);
				Console.WriteLine(_empiConnectorResponse.StatusCode.ToString() + " " + ex.Message);

				return _empiConnectorResponse;
			}
		}
	}
}
