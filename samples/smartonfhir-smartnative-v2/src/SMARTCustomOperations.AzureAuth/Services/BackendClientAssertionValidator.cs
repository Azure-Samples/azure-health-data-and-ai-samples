// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// SMART v2 backend services <c>client_assertion</c> validator (private_key_jwt).
    /// Enforces: <c>kid</c>, <c>iss==sub==clientId</c>, <c>jti</c>, <c>iat</c>, <c>exp</c>,
    /// max lifetime, algorithm whitelist, optional <c>jku</c> binding, signature against
    /// the client's registered JWKS, audience equal to the proxy token URL, and jti replay.
    /// </summary>
    public sealed class BackendClientAssertionValidator : IBackendClientAssertionValidator
    {
        private const string JwtBearerClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

        // SMART v2 backend services: assertion lifetime must not exceed 5 minutes.
        // See http://hl7.org/fhir/smart-app-launch/backend-services.html
        private static readonly TimeSpan MaxAssertionLifetime = TimeSpan.FromMinutes(5);

        // SMART v2 backend services: only asymmetric algorithms are permitted.
        // ES384 (ECDSA P-384 / SHA-384) and RS384 (RSA / SHA-384) are the spec-compliant set.
        private static readonly HashSet<string> AllowedAssertionAlgs =
            new(StringComparer.Ordinal) { "ES384", "RS384" };

        private readonly IClientConfigService _clientConfigService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAssertionReplayProtector _replayProtector;
        private readonly ILogger<BackendClientAssertionValidator> _logger;

        public BackendClientAssertionValidator(
            IClientConfigService clientConfigService,
            IHttpClientFactory httpClientFactory,
            IAssertionReplayProtector replayProtector,
            ILogger<BackendClientAssertionValidator> logger)
        {
            _clientConfigService = clientConfigService;
            _httpClientFactory = httpClientFactory;
            _replayProtector = replayProtector;
            _logger = logger;
        }

        public async Task<BackendClientConfiguration> ValidateAsync(
            string clientId,
            string clientAssertionType,
            string clientAssertion,
            string expectedAudience)
        {
            if (!string.Equals(clientAssertionType, JwtBearerClientAssertionType, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Unsupported client_assertion_type.");
            }

            var token = ReadJwt(clientAssertion);

            if (string.IsNullOrWhiteSpace(token.Header.Kid))
            {
                throw new UnauthorizedAccessException("client_assertion header is missing kid.");
            }

            if (!string.Equals(token.Issuer, clientId, StringComparison.Ordinal) ||
                !string.Equals(token.Subject, clientId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("client_assertion must satisfy iss=sub=client_id.");
            }

            if (string.IsNullOrWhiteSpace(token.Id))
            {
                throw new UnauthorizedAccessException("client_assertion must include jti.");
            }

            var issuedAt = token.IssuedAt == DateTime.MinValue
                ? (DateTime?)null
                : token.IssuedAt.ToUniversalTime();

            if (issuedAt is null)
            {
                throw new UnauthorizedAccessException("client_assertion must include iat.");
            }

            if (token.ValidTo == DateTime.MinValue)
            {
                throw new UnauthorizedAccessException("client_assertion must include exp.");
            }

            var actualLifetime = token.ValidTo.ToUniversalTime() - issuedAt.Value;
            if (actualLifetime <= TimeSpan.Zero || actualLifetime > MaxAssertionLifetime)
            {
                throw new UnauthorizedAccessException(
                    $"client_assertion lifetime must be between 1 and {MaxAssertionLifetime.TotalSeconds:0} seconds.");
            }

            var clientConfig = await _clientConfigService.FetchBackendClientConfiguration(clientId);

            // Optional jku must match registered JWKS URI.
            if (token.Header.TryGetValue("jku", out var jkuValue) &&
                jkuValue is string jkuString && !string.IsNullOrWhiteSpace(jkuString))
            {
                if (!Uri.TryCreate(jkuString, UriKind.Absolute, out var jkuUri) ||
                    Uri.Compare(jkuUri, clientConfig.JwksUri, UriComponents.HttpRequestUrl, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    throw new UnauthorizedAccessException("client_assertion jku does not match registered jwks_url.");
                }
            }

            if (!AllowedAssertionAlgs.Contains(token.Header.Alg))
            {
                throw new UnauthorizedAccessException(
                    $"JWT alg '{token.Header.Alg}' is not allowed.");
            }

            var jwks = await FetchJwks(clientConfig.JwksUri);
            var signingKeys = jwks.GetSigningKeys();

            var parameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience = expectedAudience,
                ValidateIssuer = true,
                ValidIssuer = clientId,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                RequireSignedTokens = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(clientAssertion, parameters, out _);

            var expiresAt = new DateTimeOffset(token.ValidTo.ToUniversalTime());
            if (!_replayProtector.TryRegister(clientId, token.Id, expiresAt))
            {
                throw new UnauthorizedAccessException("client_assertion jti replay detected.");
            }

            return clientConfig;
        }

        private static JwtSecurityToken ReadJwt(string token)
        {
            try
            {
                return new JwtSecurityTokenHandler().ReadJwtToken(token);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("Invalid client_assertion JWT.", ex);
            }
        }

        private async Task<JsonWebKeySet> FetchJwks(Uri jwksUri)
        {
            var client = _httpClientFactory.CreateClient();
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(jwksUri);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch JWKS from {JwksUri}", jwksUri);
                throw new UnauthorizedAccessException("Unable to fetch client JWKS.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UnauthorizedAccessException(
                    $"Failed to fetch client JWKS from {jwksUri}. HTTP {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadAsStringAsync();
            try
            {
                return new JsonWebKeySet(body);
            }
            catch (JsonException ex)
            {
                throw new UnauthorizedAccessException("Client JWKS payload is not valid JSON.", ex);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("Client JWKS is invalid.", ex);
            }
        }
    }
}
