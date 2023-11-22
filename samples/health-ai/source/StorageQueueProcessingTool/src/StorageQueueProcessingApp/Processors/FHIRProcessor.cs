using Microsoft.Extensions.Logging;
using Polly;
using StorageQueueProcessingApp.Configuration;
using StorageQueueProcessingApp.FHIRClient;
using System.Net;
using System.Text;

namespace StorageQueueProcessingApp.Processors
{
	public class FHIRProcessor : IFHIRProcessor
	{

		private readonly ILogger<FHIRProcessor>? _logger;
		private readonly IFHIRClient _fhirClient;
		private readonly ProcessorConfig config;
		private static readonly HttpStatusCode[] httpStatusCodesWorthRetrying = {
			HttpStatusCode.RequestTimeout, // 408
            HttpStatusCode.InternalServerError, // 500
            HttpStatusCode.BadGateway, // 502
            HttpStatusCode.ServiceUnavailable, // 503
            HttpStatusCode.GatewayTimeout, // 504
            HttpStatusCode.TooManyRequests //429
        };

		public FHIRProcessor(IFHIRClient fhirClient, ILogger<FHIRProcessor>? logger, ProcessorConfig _config)
		{
			_logger = logger;
			_fhirClient = fhirClient;
			config = _config;
		}
		public async Task<HttpResponseMessage> CallFHIRMethod(string body, HttpMethod method, string endpoint)
		{
			try
			{
				var _fhirRequest = new HttpRequestMessage
				{
					Method = method,
					RequestUri = new Uri(config.FhirUri.ToString()),
					Headers =
					{
						{ HttpRequestHeader.Accept.ToString(), "application/fhir+json" },
						{ "Prefer", "respond-async" },
					},
					Content = new StringContent(body, Encoding.UTF8, "application/fhir+json"),
				};

				var retryPolicy = Policy
					.Handle<HttpRequestException>()
					.OrResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
					.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(500), (result, timeSpan, retryCount, context) =>
					{
						_logger?.LogWarning($"FHIR Request failed on a retryable status...Waiting {timeSpan} before next retry. Attempt {retryCount}");
					});
				HttpResponseMessage responseMessage = await retryPolicy.ExecuteAsync(async () =>
				{
					return await _fhirClient.Send(_fhirRequest, endpoint);
				});
				if (responseMessage.IsSuccessStatusCode)
				{
					_logger?.LogInformation("FHIR json file uploaded successfully!");
				}
				return responseMessage;
			}
			catch
			{
				_logger?.LogError($"Error occurred while Uploading FHIR json.");
				throw;
			}
		}
	}
}
