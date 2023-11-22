using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Dicom.Client;
using StorageQueueProcessingApp.Configuration;
using StorageQueueProcessingApp.Processors;
using System.Text.Json.Nodes;

namespace StorageQueueProcessingApp
{
	public class ImportHandler
	{
		private ProcessorConfig config;
		private IFHIRProcessor fhirProcessor;
		private IDICOMProcessor dicomProcessor;
		private IBlobProcessor blobProcessor;
		private TelemetryClient telemetryClient;
		private ILogger logger;

		public ImportHandler(ProcessorConfig config, IFHIRProcessor fhirProcessor, IDICOMProcessor dicomProcessor, IBlobProcessor blobProcessor, TelemetryClient telemetryClient, ILoggerFactory loggerFactory)
		{
			this.config = config;
			this.fhirProcessor = fhirProcessor;
			this.dicomProcessor = dicomProcessor;
			this.blobProcessor = blobProcessor;
			this.telemetryClient = telemetryClient;
			this.logger = loggerFactory.CreateLogger<ImportHandler>();
		}

		public async Task ImportFile(JsonObject blobCreatedEvent)
		{
			string ImportQueueId = (string)blobCreatedEvent["id"];
			string url = (string)blobCreatedEvent["data"]["url"];
			logger.LogInformation($"ImportBundleEventGrid: Processing blob at {url}...");
			string container = config.sourceContainerName;
			string destContainerName = config.processedContainerName;
			string fileName = url.Substring(url.IndexOf($"/{container}/") + $"/{container}/".Length);
			string fileType = fileName.Substring(fileName.LastIndexOf(".") + 1);
			telemetryClient.TrackEvent("ImportQueue", new Dictionary<string, string>
			{
				{ "ImportQueueId", ImportQueueId },
				{ "fileName", fileName },
				{ "fileType", fileType },
				{ "Status", "Started" },
				{ "Since", (string)blobCreatedEvent["eventType"] }
			});
			logger.LogInformation($"ImportFile: Processing file Name:{fileName}...");

			try
			{
				var cbclient = blobProcessor.GetCloudBlobClient();
				if (fileType.Equals("json"))
				{
					Stream myBlob = await blobProcessor.GetStreamForBlob(cbclient, container, fileName);
					if (myBlob == null)
					{
						logger.LogWarning($"ImportFile:The blob {fileName} in container {container} does not exist or cannot be read.");
						return;
					}
					string trtext = "";
					using (StreamReader reader = new StreamReader(myBlob))
					{
						trtext = await reader.ReadToEndAsync();
					}
					// Send the FHIR content to the FHIR service using FhirClient
					HttpResponseMessage response = await fhirProcessor.CallFHIRMethod(trtext, HttpMethod.Post, config.FhirHttpClient);
					if (response.IsSuccessStatusCode)
					{
						telemetryClient.TrackEvent("ImportQueue", new Dictionary<string, string>
						{
							{ "ImportQueueId", ImportQueueId },
							{ "fileName", fileName },
							{ "fileType", fileType },
							{ "Status", "Completed" },
							{ "Till", DateTime.Now.ToString() }
						});
						await blobProcessor.MoveTo(cbclient, container, destContainerName, fileName, fileName, logger);
					}
				}
				else
				{
					BlobClient blobClient = blobProcessor.GetBlobClient(container, fileName);
					if (blobClient.Exists())
					{
						// Download the DICOM file as a MemoryStream
						using (Stream dicomStream = new MemoryStream())
						{
							await blobClient.OpenReadAsync();
							await blobClient.DownloadToAsync(dicomStream);
							dicomStream.Seek(0, SeekOrigin.Begin);

							// Send the DICOM content to the DICOM service using DicomClient
							DicomWebResponse respone = await dicomProcessor.CallDICOMMethod(dicomStream);
							if (respone.IsSuccessStatusCode)
							{
								telemetryClient.TrackEvent("ImportQueue", new Dictionary<string, string>
								{
									{ "ImportQueueId", ImportQueueId },
									{ "fileName", fileName },
									{ "fileType", fileType },
									{ "Status", "Completed" },
									{ "Till", DateTime.Now.ToString() }
								});
								await blobProcessor.MoveTo(cbclient, container, destContainerName, fileName, fileName, logger);
							}
						}
					}
					else
					{
						logger.LogWarning($"ImportFile:The blob {fileName} in container {container} does not exist or cannot be read.");
						return;
					}
				}
			}
			catch
			{
				logger.LogError($"ImportFile:Unhandled Exception on file {fileName}");
				throw;
			}
		}
	}
}
