using Microsoft.Extensions.Logging;
using Microsoft.Health.Dicom.Client;
using StorageQueueProcessingApp.Configuration;

namespace StorageQueueProcessingApp.DICOMClient
{
	public class DICOMClient : IDICOMClient
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly IDicomWebClient client;
		private readonly ProcessorConfig config;
		private readonly ILogger<DICOMClient>? logger;

		public DICOMClient(IHttpClientFactory httpClientfactory, ProcessorConfig _config, ILogger<DICOMClient>? _logger)
		{
			_httpClientFactory = httpClientfactory;
			config = _config;
			logger = _logger;
			client = new DicomWebClient(_httpClientFactory.CreateClient(config.DicomHttpClient));
		}

		public async Task<DicomWebResponse> Store(Stream dicomStream)
		{
			try
			{
				DicomWebResponse response = await client.StoreAsync(new[] { dicomStream });
				return response;
			}
			catch (DicomWebException ex)
			{
				logger?.LogError($"Dicom File upload failed with error {ex.Message}");
				throw;
			}
			catch
			{
				throw;
			}
		}
	}
}
