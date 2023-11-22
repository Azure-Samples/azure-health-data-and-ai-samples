using Microsoft.Extensions.Logging;
using Microsoft.Health.Dicom.Client;
using Polly;
using StorageQueueProcessingApp.DICOMClient;
using System.Net;

namespace StorageQueueProcessingApp.Processors
{
	public class DICOMProcessor : IDICOMProcessor
	{
		private readonly ILogger<DICOMProcessor>? _logger;
		private readonly IDICOMClient _dicomClient;
		private static readonly HttpStatusCode[] httpStatusCodesWorthRetrying = {
			HttpStatusCode.RequestTimeout, // 408
            HttpStatusCode.InternalServerError, // 500
            HttpStatusCode.BadGateway, // 502
            HttpStatusCode.ServiceUnavailable, // 503
            HttpStatusCode.GatewayTimeout, // 504
            HttpStatusCode.TooManyRequests //429
        };

		public DICOMProcessor(IDICOMClient dicomClient, ILogger<DICOMProcessor>? logger)
		{
			_dicomClient = dicomClient;
			_logger = logger;
		}
		public async Task<DicomWebResponse> CallDICOMMethod(Stream dicomStream)
		{
			try
			{
				var retryPolicy = Policy
					.Handle<HttpRequestException>()
					.OrResult<DicomWebResponse>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
					.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(500), (result, timeSpan, retryCount, context) =>
					{
						_logger?.LogWarning($"DICOM Request failed on a retryable status...Waiting {timeSpan} before next retry. Attempt {retryCount}");
					});
				DicomWebResponse response = await retryPolicy.ExecuteAsync(async () =>
				{
					return await _dicomClient.Store(dicomStream);
				});
				if (response.IsSuccessStatusCode)
				{
					_logger?.LogInformation("DICOM file uploaded successfully!");
				}
				return response;
			}
			catch
			{
				_logger?.LogError($"Error occurred while Uploading DICOM file.");
				throw;
			}
		}
	}
}
