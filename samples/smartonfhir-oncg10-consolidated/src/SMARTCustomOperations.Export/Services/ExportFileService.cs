using Azure.Storage.Blobs;

namespace SMARTCustomOperations.Export.Services
{
    public class ExportFileService : IExportFileService
    {
        private readonly BlobServiceClient _blobServiceClient;

        public ExportFileService(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient;
        }

        public async Task<byte[]> GetContent(string containerName, string blobName)
        {
            BlobClient blobClient;

            try
            {
                // Get the blob client for the export file
                blobClient = _blobServiceClient
                    .GetBlobContainerClient(containerName)
                    .GetBlobClient(blobName);

                if (!blobClient.Exists())
                {
                    throw new ArgumentException($"Blob {blobName} does not exist in container {containerName}");
                }

                var blobContent = await blobClient.DownloadContentAsync();
                BinaryData blobBinaryContent = blobContent.Value.Content!;
                return blobBinaryContent.ToArray();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 401)
            {
                throw new SystemException("Backend service is not authorized to access the blob. Check your configuration.", ex);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                throw new ArgumentException($"Blob {blobName} does not exist in container {containerName}");
            }

            
        }
    }
}
