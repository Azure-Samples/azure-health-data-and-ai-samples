// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SMARTCustomOperations.AzureAuth.Configuration;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Validates Microsoft Entra-issued access tokens presented to the bulk export endpoints.
    /// Accepts tokens whose audience matches the FHIR audience and whose issuer is the configured
    /// Entra tenant, then derives a stable client identity from the validated token (azp / appid).
    /// </summary>
    public sealed class FhirAccessTokenValidator : IFhirAccessTokenValidator
    {
        private readonly AzureAuthOperationsConfig _config;
        private readonly ILogger<FhirAccessTokenValidator> _logger;
        private readonly Lazy<ConfigurationManager<OpenIdConnectConfiguration>> _oidcConfig;
        private readonly JwtSecurityTokenHandler _handler = new();

        public FhirAccessTokenValidator(AzureAuthOperationsConfig config, ILogger<FhirAccessTokenValidator> logger)
        {
            _config = config;
            _logger = logger;
            _oidcConfig = new Lazy<ConfigurationManager<OpenIdConnectConfiguration>>(() =>
            {
                var authority = $"https://login.microsoftonline.com/{_config.TenantId}/v2.0";
                return new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{authority}/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever());
            });
        }

        public async Task<FhirTokenValidationResult> ValidateAsync(string? authorizationHeaderValue, CancellationToken cancellationToken = default)
        {
            var token = ExtractBearerToken(authorizationHeaderValue);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new UnauthorizedAccessException("Missing bearer token.");
            }

            if (!_handler.CanReadToken(token))
            {
                throw new UnauthorizedAccessException("Malformed bearer token.");
            }

            OpenIdConnectConfiguration oidc;
            try
            {
                oidc = await _oidcConfig.Value.GetConfigurationAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Entra OpenID configuration for token validation.");
                throw new InvalidOperationException("Unable to retrieve token signing metadata.", ex);
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudiences = BuildValidAudiences(),
                ValidateIssuer = true,
                ValidIssuers = BuildValidIssuers(),
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = oidc.SigningKeys,
                ValidateLifetime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            ClaimsPrincipal principal;
            try
            {
                principal = _handler.ValidateToken(token, validationParameters, out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FHIR access token rejected: {Message}", ex.Message);
                throw new UnauthorizedAccessException("Invalid access token.", ex);
            }

            var clientId = ExtractClientId(principal);
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new UnauthorizedAccessException("Access token does not identify a client (azp/appid).");
            }

            return new FhirTokenValidationResult(principal, clientId);
        }

        private static string? ExtractBearerToken(string? authorizationHeaderValue)
        {
            if (string.IsNullOrWhiteSpace(authorizationHeaderValue))
            {
                return null;
            }

            const string prefix = "Bearer ";
            return authorizationHeaderValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? authorizationHeaderValue.Substring(prefix.Length).Trim()
                : authorizationHeaderValue.Trim();
        }

        private static string? ExtractClientId(ClaimsPrincipal principal)
        {
            // v2 tokens: azp. v1 tokens: appid. Fall back to the service principal object id.
            return principal.FindFirst("azp")?.Value
                ?? principal.FindFirst("appid")?.Value
                ?? principal.FindFirst("oid")?.Value;
        }

        private string[] BuildValidAudiences()
        {
            var audiences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddWithVariants(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                var trimmed = value.TrimEnd('/');
                audiences.Add(trimmed);
                audiences.Add(trimmed + "/");
            }

            AddWithVariants(_config.FhirAudience);
            AddWithVariants(_config.FhirServerUrl);

            if (audiences.Count == 0)
            {
                throw new InvalidOperationException("FhirAccessTokenValidator has no valid audience. Configure FhirAudience.");
            }

            return audiences.ToArray();
        }

        private string[] BuildValidIssuers()
        {
            var tenantId = _config.TenantId!;
            return new[]
            {
                $"https://login.microsoftonline.com/{tenantId}/",
                $"https://login.microsoftonline.com/{tenantId}/v2.0",
                $"https://sts.windows.net/{tenantId}/",
            };
        }
    }
}
