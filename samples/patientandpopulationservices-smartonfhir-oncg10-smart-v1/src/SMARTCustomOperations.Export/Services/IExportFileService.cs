namespace SMARTCustomOperations.Export.Services
{
    public interface IExportFileService
    {
        public Task<byte[]> GetContent(string containerName, string blobName);
    }
}
