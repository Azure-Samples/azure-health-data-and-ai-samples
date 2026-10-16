// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Specialized;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Default <see cref="IClientAssertionAuthenticator"/>. Reuses the SMART v2 backend-services
    /// assertion validator (JWKS signature, <c>iss=sub=client_id</c>, <c>aud</c>, lifetime, <c>jti</c>
    /// replay) and the Key Vault client registration to swap a <c>private_key_jwt</c> for the stored
    /// Entra <c>client_secret</c>. Registered only when Entra is the upstream IdP and a Key Vault
    /// store is configured.
    /// </summary>
    public sealed class ClientAssertionAuthenticator : IClientAssertionAuthenticator
    {
        public const string JwtBearerClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

        private static readonly JwtSecurityTokenHandler Handler = new();

        private readonly IBackendClientAssertionValidator _assertionValidator;
        private readonly ILogger<ClientAssertionAuthenticator> _logger;

        public ClientAssertionAuthenticator(
            IBackendClientAssertionValidator assertionValidator,
            ILogger<ClientAssertionAuthenticator> logger)
        {
            _assertionValidator = assertionValidator;
            _logger = logger;
        }

        public async Task SwapAssertionForClientSecretAsync(NameValueCollection formData, string expectedAudience)
        {
            ArgumentNullException.ThrowIfNull(formData);

            var clientAssertion = formData["client_assertion"];
            var clientAssertionType = formData["client_assertion_type"] is { } rawType
                ? Uri.UnescapeDataString(rawType)
                : null;

            if (string.IsNullOrEmpty(clientAssertion) ||
                !string.Equals(clientAssertionType, JwtBearerClientAssertionType, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("A jwt-bearer client_assertion is required.");
            }

            // SMART v2 asymmetric clients set iss=sub=client_id; the client_id is the assertion subject.
            var clientId = ReadSubject(clientAssertion);
            if (string.IsNullOrEmpty(clientId))
            {
                throw new UnauthorizedAccessException("client_assertion is missing the sub claim.");
            }

            // Full validation (signature via registered JWKS, aud, lifetime, jti replay) + Key Vault lookup.
            var clientConfig = await _assertionValidator.ValidateAsync(
                clientId,
                clientAssertionType!,
                clientAssertion,
                expectedAudience);

            // Present the request to Entra as a confidential client: drop the asymmetric proof and
            // inject the stored symmetric secret. client_id is (re)set from the assertion subject
            // because asymmetric clients need not send it in the body.
            formData.Remove("client_assertion");
            formData.Remove("client_assertion_type");
            formData.Remove("client_id");
            formData.Add("client_id", clientId);
            formData.Remove("client_secret");
            formData.Add("client_secret", clientConfig.ClientSecret);

            _logger.LogInformation(
                "Swapped private_key_jwt client_assertion for stored client_secret for client {ClientId}.",
                clientId);
        }

        private static string ReadSubject(string clientAssertion)
        {
            try
            {
                return Handler.ReadJwtToken(clientAssertion).Subject;
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("client_assertion is not a valid JWT.", ex);
            }
        }
    }
}
