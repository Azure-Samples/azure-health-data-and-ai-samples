// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Deterministic mapping from a validated client identity to its dedicated export blob
    /// container. Used both when initiating an export (to set <c>_container</c>) and when serving
    /// files (to verify the caller owns the container), guaranteeing identical isolation on both
    /// paths.
    /// </summary>
    public static class ExportContainerNaming
    {
        /// <summary>
        /// Derives a stable, valid blob container name (33 chars, lowercase alphanumeric) unique
        /// per client from a hash of the validated client id.
        /// </summary>
        public static string ForClient(string clientId)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(clientId.ToLowerInvariant()));
            var hex = Convert.ToHexString(hash).ToLowerInvariant();
            return "e" + hex[..32];
        }
    }
}
