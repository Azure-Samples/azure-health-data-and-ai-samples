// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Streams export NDJSON files from Azure Blob Storage using the gateway managed identity.
    /// </summary>
    public sealed class BlobExportFileService : IExportFileService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobExportFileService> _logger;

        public BlobExportFileService(AzureAuthOperationsConfig config, ILogger<BlobExportFileService> logger)
        {
            if (string.IsNullOrWhiteSpace(config.ExportStorageBlobUri))
            {
                throw new InvalidOperationException("BlobExportFileService requires ExportStorageBlobUri to be configured.");
            }

            _logger = logger;
            _blobServiceClient = new BlobServiceClient(new Uri(config.ExportStorageBlobUri), new DefaultAzureCredential());
        }

        public async Task<Stream?> OpenReadAsync(string containerName, string blobPath, CancellationToken cancellationToken = default)
        {
            var blobClient = _blobServiceClient
                .GetBlobContainerClient(containerName)
                .GetBlobClient(blobPath);

            try
            {
                return await blobClient.OpenReadAsync(new Azure.Storage.Blobs.Models.BlobOpenReadOptions(allowModifications: false), cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
            catch (RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
            {
                _logger.LogError(ex, "Gateway identity is not authorized to read export blob {Container}/{Blob}.", containerName, blobPath);
                throw new UnauthorizedAccessException("Gateway is not authorized to read the export file.", ex);
            }
        }
    }
}
