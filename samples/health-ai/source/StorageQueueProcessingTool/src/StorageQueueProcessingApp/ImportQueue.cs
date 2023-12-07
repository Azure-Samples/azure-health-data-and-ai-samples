using System.Text.Json.Nodes;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using StorageQueueProcessingApp.Configuration;
using StorageQueueProcessingApp.DICOMClient;
using StorageQueueProcessingApp.FHIRClient;
using StorageQueueProcessingApp.Processors;

namespace StorageQueueProcessingApp
{
	public class ImportQueue
    {
		private readonly ILogger _logger;
		private readonly IFHIRProcessor _fhirProcessor;
		private readonly IDICOMProcessor _dicomProcessor;
		private readonly ProcessorConfig _config;
		private readonly IBlobProcessor _blobProcessor;
		private readonly TelemetryClient _telemetryClient;
		private readonly ImportHandler _importHandler;

		public ImportQueue(ILoggerFactory loggerFactory, ProcessorConfig processorConfig, IFHIRProcessor fhirProcessor, IDICOMProcessor dicomProcessor, IFHIRClient fhirClient, IDICOMClient dicomClient, IBlobProcessor blobProcessor, TelemetryClient telemetryClient)
        {
			_logger = loggerFactory.CreateLogger<ImportQueue>();
			_fhirProcessor = fhirProcessor;
			_dicomProcessor = dicomProcessor;
			_blobProcessor = blobProcessor;
			_telemetryClient = telemetryClient;
			_config = processorConfig;
			_importHandler = new ImportHandler(_config, _fhirProcessor, _dicomProcessor, _blobProcessor, _telemetryClient, loggerFactory);
		}

		[Function("ImportQueue")]
		public async Task Run([QueueTrigger( "%queueName%")] JsonObject blobCreatedEvent)
		{
			_logger.LogInformation("ImportQueue Triggered");
			try
			{
				await _importHandler.ImportFile(blobCreatedEvent);
			}
			catch(Exception ex)
			{
				_logger.LogError($"Import Handler Failed with Exception : {ex.Message}", ex);
			}
			_logger.LogInformation("ImportQueue completed.");
		}
	}
}
