// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Maintains a blocklist of revoked access tokens so the gateway can reject them on FHIR
    /// requests even though Microsoft Entra cannot natively revoke a (self-contained) access token.
    /// Tokens are identified by their unique token id (Entra "uti", or "jti" fallback).
    /// </summary>
    public interface ITokenBlocklistService
    {
        /// <summary>Marks the token id as revoked. Idempotent.</summary>
        Task BlockAsync(string tokenId);

        /// <summary>Returns true if the token id has been revoked.</summary>
        Task<bool> IsBlockedAsync(string tokenId);
    }
}
