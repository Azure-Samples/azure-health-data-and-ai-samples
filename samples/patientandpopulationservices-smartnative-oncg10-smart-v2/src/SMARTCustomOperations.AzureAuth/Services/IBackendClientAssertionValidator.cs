// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Validates a SMART v2 backend services <c>client_assertion</c> JWT (private_key_jwt).
    /// Returns the backend client configuration (Entra client_secret + JWKS URI) on success.
    /// Throws <see cref="UnauthorizedAccessException"/> on any validation failure.
    /// </summary>
    public interface IBackendClientAssertionValidator
    {
        Task<BackendClientConfiguration> ValidateAsync(
            string clientId,
            string clientAssertionType,
            string clientAssertion,
            string expectedAudience);
    }
}
