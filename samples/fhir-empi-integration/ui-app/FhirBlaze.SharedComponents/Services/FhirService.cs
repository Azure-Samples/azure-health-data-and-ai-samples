using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents.Services
{
	public class FhirService : IFhirService
	{
		private readonly FhirDataConnection _connection;
		private readonly IHttpHelperMethods _httpHelper;
		private readonly ILogger<FhirService> _logger;

		public FhirService(FhirDataConnection connection, IHttpHelperMethods httpHelper, ILogger<FhirService> logger)
		{
			_connection = connection;
			_httpHelper = httpHelper;
			_logger = logger;
		}

		public async Task<HttpResponseMessage> CreatePatientsAsync(string patient)
		{
			HttpResponseMessage httpResponseMessage = null;
			try
			{
				_logger.LogInformation($"Calling PostAsync : {_connection.FhirServerUri}/Patient");
				httpResponseMessage = await _httpHelper.PostAsync($"{_connection.FhirServerUri}/Patient", patient, true);
				_logger.LogInformation($"Response: {httpResponseMessage.Content}");
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

		public async Task<HttpResponseMessage> DeletePatientAsync(string patientId)
		{
			HttpResponseMessage httpResponseMessage = null;
			try
			{
				_logger.LogInformation($"Calling DeleteAsync : {_connection.FhirServerUri}/Patient/{patientId}");
				httpResponseMessage = await _httpHelper.DeleteAsync($"{_connection.FhirServerUri}/Patient/{patientId}");
				_logger.LogInformation($"Response: {httpResponseMessage.Content}");
			}
			catch (Exception ex) {
				httpResponseMessage.StatusCode = (HttpStatusCode)500;
				httpResponseMessage.Content = new StringContent(ex.Message);
				_logger.LogError(httpResponseMessage.StatusCode.ToString() + " " + ex.Message);
				throw;
			}
			return httpResponseMessage;
		}

		public async Task<HttpResponseMessage> UpdatePatientAsync(string patientId, string patient)
		{
			HttpResponseMessage httpResponseMessage = null;
			try
			{
				_logger.LogInformation($"Calling UpdateAsync : {_connection.FhirServerUri}/Patient/{patientId}");
				httpResponseMessage = await _httpHelper.PutAsync($"{_connection.FhirServerUri}/Patient/{patientId}", patient);
				_logger.LogInformation($"Response: {httpResponseMessage.Content}");
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
	}
}
