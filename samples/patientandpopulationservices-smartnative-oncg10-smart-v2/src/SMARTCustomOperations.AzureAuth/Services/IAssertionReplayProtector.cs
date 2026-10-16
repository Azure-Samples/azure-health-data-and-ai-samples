// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Tracks seen <c>jti</c> values per backend client to satisfy SMART v2 replay protection.
    /// </summary>
    public interface IAssertionReplayProtector
    {
        /// <summary>
        /// Registers a (clientId, jti) pair until <paramref name="expiresAtUtc"/>.
        /// Returns false if the pair was already seen (replay) or the TTL is non-positive.
        /// </summary>
        bool TryRegister(string clientId, string jti, DateTimeOffset expiresAtUtc);
    }
}
