using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
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
			BlobServiceClient blobServiceClient = new BlobServiceClient(_config.StorageConnection);
			BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
			return containerClient.GetBlobClient(fileName);
		}
		public CloudBlobClient GetCloudBlobClient()
		{
			var storageAccount = CloudStorageAccount.Parse(_config.StorageConnection);
			return storageAccount.CreateCloudBlobClient();
		}
		public async Task<Stream> GetStreamForBlob(CloudBlobClient blobClient, string containerName, string filePath)
		{
			var sourceContainer = blobClient.GetContainerReference(containerName);
			CloudBlob sourceBlob = sourceContainer.GetBlobReference(filePath);
			if (await sourceBlob.ExistsAsync())
			{
				return await sourceBlob.OpenReadAsync();
			}
			return null;
		}
		/*Moves Source File in Container to Destination Container and Deletes Source - Same Storage Account*/
		public async Task MoveTo(CloudBlobClient blobClient, string sourceContainerName, string destContainerName, string name, string destName, ILogger log)
		{
			try
			{
				// details of our source file
				var sourceFilePath = name;

				// details of where we want to copy to
				var destFilePath = destName;

				var sourceContainer = blobClient.GetContainerReference(sourceContainerName);
				var destContainer = blobClient.GetContainerReference(destContainerName);
				if (!await destContainer.ExistsAsync())
				{
					await destContainer.CreateAsync();
				}

				CloudBlob sourceBlob = sourceContainer.GetBlobReference(sourceFilePath);
				CloudBlob destBlob = destContainer.GetBlobReference(destFilePath);

				string copyid = await destBlob.StartCopyAsync(sourceBlob.Uri);
				//fetch current attributes
				await destBlob.FetchAttributesAsync();
				//waiting for completion
				int copyretries = 5;
				while (destBlob.CopyState.Status == CopyStatus.Pending && copyretries > 1)
				{
					await Task.Delay(500);
					await destBlob.FetchAttributesAsync();
					copyretries--;
				}
				if (destBlob.CopyState.Status != CopyStatus.Success)
				{
					log.LogError($"Copy failed file {name} to {destName}!");
					await destBlob.AbortCopyAsync(copyid);
					return;
				}
				await sourceBlob.DeleteAsync();
			}
			catch (Exception e)
			{
				log.LogError($"Error Moving file {name} to {destName}:{e.Message}");
				throw;
			}
		}
	}
}
