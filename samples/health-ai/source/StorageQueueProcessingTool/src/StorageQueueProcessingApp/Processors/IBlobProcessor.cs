using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace StorageQueueProcessingApp.Processors
{
	public interface IBlobProcessor
	{

		BlobServiceClient GetBlobServiceClient();

		Task<Stream> GetStreamForBlob(BlobServiceClient blobServiceClient, string containerName, string filePath);

		Task MoveTo(BlobServiceClient blobServiceClient, string sourceContainerName, string destContainerName, string name, string destName, ILogger log);

        BlobClient GetBlobClient(string containerName, string fileName);
	}
}
