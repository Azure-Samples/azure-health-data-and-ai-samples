using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using StorageQueueProcessingApp.Configuration;


namespace StorageQueueProcessingApp.Processors
{
	public class BlobProcessor : IBlobProcessor
	{
		private readonly ProcessorConfig _config;

        public BlobProcessor(ProcessorConfig config)
		{
			_config = config;
		}
		public BlobClient GetBlobClient(string containerName, string fileName)
		{
			BlobServiceClient blobServiceClient = new BlobServiceClient(new Uri(_config.StorageUri), new DefaultAzureCredential());
			BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
			return containerClient.GetBlobClient(fileName);
		}

        public BlobServiceClient GetBlobServiceClient()
        {
            return new BlobServiceClient(new Uri(_config.StorageUri), new DefaultAzureCredential());
        }

        public async Task<Stream> GetStreamForBlob(BlobServiceClient blobServiceClient, string containerName, string filePath)
        {
            var sourceContainer = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient sourceBlob = sourceContainer.GetBlobClient(filePath);
            if (await sourceBlob.ExistsAsync())
            {
                return await sourceBlob.OpenReadAsync();
            }
            return null;
        }

        public async Task MoveTo(BlobServiceClient blobServiceClient, string sourceContainerName, string destContainerName, string name, string destName, ILogger log)
        {
            try
            {
                // details of our source file
                var sourceFilePath = name;

                // details of where we want to copy to
                var destFilePath = destName;

                var sourceContainer = blobServiceClient.GetBlobContainerClient(sourceContainerName);
                var destContainer = blobServiceClient.GetBlobContainerClient(destContainerName);
                if (!await destContainer.ExistsAsync())
                {
                    await destContainer.CreateAsync();
                }

                BlobClient sourceBlob = sourceContainer.GetBlobClient(sourceFilePath);
                BlobClient destBlob = destContainer.GetBlobClient(destFilePath);

                var copyOp = await destBlob.StartCopyFromUriAsync(sourceBlob.Uri);
                
                await WaitForCopyCompletionAsync(destBlob, copyOp.Id);
                
                await sourceBlob.DeleteIfExistsAsync();
            }
            catch (Exception e)
            {
                log.LogError($"Error Moving file {name} to {destName}:{e.Message}");
                throw;
            }
        }

        private async Task WaitForCopyCompletionAsync(BlobClient blobClient, string copyId)
        {
            while (true)
            {
                var properties = await blobClient.GetPropertiesAsync();
                var copyStatus = properties.Value.CopyStatus;

                if (copyStatus == CopyStatus.Success)
                {
                    break;
                }
                else if (copyStatus == CopyStatus.Failed || copyStatus == CopyStatus.Aborted)
                {
                    throw new InvalidOperationException("Copy operation failed or was aborted.");
                }

                await Task.Delay(1000); // Wait for 1 second before checking again
            }
        }

    }
}
