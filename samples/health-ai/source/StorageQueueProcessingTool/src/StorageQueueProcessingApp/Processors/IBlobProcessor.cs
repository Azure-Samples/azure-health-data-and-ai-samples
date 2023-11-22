using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace StorageQueueProcessingApp.Processors
{
	public interface IBlobProcessor
	{
		CloudBlobClient GetCloudBlobClient();
		BlobClient GetBlobClient(string containerName, string fileName);
		Task<Stream> GetStreamForBlob(CloudBlobClient blobClient, string containerName, string filePath);
		Task MoveTo(CloudBlobClient blobClient, string sourceContainerName, string destContainerName, string name, string destName, ILogger log);
	}
}
