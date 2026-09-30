// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Streams bulk export NDJSON files from the export storage account. Implementations stream
    /// rather than buffer so large export files do not exhaust Function App memory.
    /// </summary>
    public interface IExportFileService
    {
        /// <summary>
        /// Opens a read stream over the given blob, or returns null if it does not exist.
        /// </summary>
        Task<Stream?> OpenReadAsync(string containerName, string blobPath, CancellationToken cancellationToken = default);
    }
}
