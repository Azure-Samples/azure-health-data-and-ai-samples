using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json;

namespace StorageQueueProcessingApp.Configuration
{
	public class ProcessorConfig
	{
		[JsonProperty("fhirUri")]
		public Uri FhirUri { get; set; }

		[JsonProperty("dicomUri")]
		public Uri DicomUri { get; set; }

		[JsonProperty("dicomResourceUri")]
		public Uri DicomResourceUri { get; set; }

		[JsonProperty("fhirHttpClient")]
		public string FhirHttpClient { get; set; } = "FhirEndpoint";

		[JsonProperty("dicomHttpClient")]
		public string DicomHttpClient { get; set; } = "DicomEndpoint";

		[JsonProperty("storageAccountName")]
		public string StorageAccountName { get; set; } = string.Empty;

		[JsonProperty("storageUri")]
		public string StorageUri { get; set; } = string.Empty;

        [JsonProperty("AppInsightConnectionString")]
		public string AppInsightConnectionstring { get; set; } = string.Empty;

		[JsonProperty("UserAgent")]
		public string UserAgent { get; set; } = "StorageQueueProcessingApp";

		[JsonProperty("sourceContainerName")]
		public string sourceContainerName { get; set; } = "ingest";

		[JsonProperty("processedContainerName")]
		public string processedContainerName { get; set; } = "processed";
		
		[JsonProperty("queueName")]
		public string queueName { get; set; } = string.Empty;

		[JsonProperty("fhirTokenCredential")]
		public TokenCredential FhirTokenCredential { get; set; } = new DefaultAzureCredential();

		[JsonProperty("debug")]
		public bool Debug { get; set; }
	}
}
