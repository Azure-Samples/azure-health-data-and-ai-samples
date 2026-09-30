// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Security.Claims;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Result of validating a FHIR access token: the validated principal and the stable
    /// client identity used to scope bulk export jobs and files.
    /// </summary>
    public sealed record FhirTokenValidationResult(ClaimsPrincipal Principal, string ClientId);

    /// <summary>
    /// Cryptographically validates the access tokens that backend clients present to the
    /// bulk export endpoints (issuer, audience, signature, lifetime) and extracts a stable
    /// client identity. Unlike the old sample, no claim is trusted without validation.
    /// </summary>
    public interface IFhirAccessTokenValidator
    {
        Task<FhirTokenValidationResult> ValidateAsync(string? authorizationHeaderValue, CancellationToken cancellationToken = default);
    }
}
