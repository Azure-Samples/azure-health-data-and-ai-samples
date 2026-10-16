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
    /// Validates bearer tokens presented by the consent picker page (Phase 2+). Accepts tokens issued
    /// by the tenant whose audience matches <see cref="AzureAuthOperationsConfig.ConsentPickerAudience"/>
    /// (falling back to api://{ContextAppClientId} + the raw client id when the setting is empty).
    /// Entra IdP only.
    /// </summary>
    public sealed class ContextTokenValidator
    {
        private readonly AzureAuthOperationsConfig _config;
        private readonly ILogger<ContextTokenValidator> _logger;
        private readonly Lazy<ConfigurationManager<OpenIdConnectConfiguration>> _oidcConfig;
        private readonly JwtSecurityTokenHandler _handler = new();

        public ContextTokenValidator(AzureAuthOperationsConfig config, ILogger<ContextTokenValidator> logger)
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

        public async Task<ClaimsPrincipal> ValidateAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new UnauthorizedAccessException("Missing bearer token.");
            }

            if (_config.Debug)
            {
                Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            }

            var oidc = await _oidcConfig.Value.GetConfigurationAsync();

            var validationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidAudiences = BuildValidAudiences(),
                ValidIssuers = BuildValidIssuers(),
                IssuerSigningKeys = oidc.SigningKeys,
            };

            var principal = _handler.ValidateToken(accessToken, validationParameters, out _);
            return principal;
        }

        private IEnumerable<string> BuildValidAudiences()
        {
            var contextAppId = _config.ContextAppClientId;
            var configuredAudience = _config.ConsentPickerAudience;

            var audiences = new List<string>();
            if (!string.IsNullOrWhiteSpace(configuredAudience))
            {
                audiences.Add(configuredAudience);
            }

            if (!string.IsNullOrWhiteSpace(contextAppId))
            {
                audiences.Add(contextAppId);
                audiences.Add($"api://{contextAppId}");
            }

            if (audiences.Count == 0)
            {
                throw new InvalidOperationException(
                    "ContextTokenValidator has no valid audience. Set ConsentPickerAudience or ContextAppClientId.");
            }

            return audiences;
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
