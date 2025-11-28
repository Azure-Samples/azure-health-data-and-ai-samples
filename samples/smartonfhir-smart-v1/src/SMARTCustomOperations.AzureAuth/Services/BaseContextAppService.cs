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
using SMARTCustomOperations.AzureAuth.Models;

#pragma warning disable CA1002 // Do not expose generic lists

namespace SMARTCustomOperations.AzureAuth.Services
{
    public class BaseContextAppService
    {
        private readonly bool _debug;
        private readonly string _contextAppClientId;
        private readonly string _fhirAudience;
        private readonly string _fhirResourceAppId;
        private readonly string _authority_url;
        private readonly string _idProviderTenantId;

        public BaseContextAppService(AzureAuthOperationsConfig configuration, ILogger<BaseContextAppService> logger)
        {
            _debug = configuration.Debug;
            _contextAppClientId = configuration.ContextAppClientId!;
            _fhirAudience = configuration.FhirAudience!;
            _fhirResourceAppId = configuration.Fhir_Resource_AppId!;
            _authority_url = configuration.Authority_URL!;
            _idProviderTenantId = configuration.IDPProviderTenantId!;
        }

        // https://github.com/Azure-Samples/ms-identity-dotnet-webapi-azurefunctions/blob/master/Function/BootLoader.cs
        public async Task<ClaimsPrincipal> ValidateContextAccessTokenAsync(string accessToken)
        {
            ConfigurationManager<OpenIdConnectConfiguration> configManager =
                new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{_authority_url}/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever());

            OpenIdConnectConfiguration config = await configManager.GetConfigurationAsync();

            var validIssuers = new List<string>()
            {
                $"{config.Issuer}",
                $"https://login.windows.net/{_idProviderTenantId}/",
                $"https://login.microsoft.com/{_idProviderTenantId}/",
                $"https://sts.windows.net/{_idProviderTenantId}/",
            };

            // Debugging purposes only, set this to false for production
            if (_debug)
            {
                Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            }

            ISecurityTokenValidator tokenValidator = new JwtSecurityTokenHandler();

            // Initialize the token validation parameters
            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,

                // App Id URI and AppId of this service application are both valid audiences.
                //ValidAudiences = new[] { _contextAppClientId, $"api://{_contextAppClientId}", _fhirAudience },
                ValidAudiences = new[] { _contextAppClientId, _fhirResourceAppId, $"api://{_contextAppClientId}", $"api://{_fhirResourceAppId}", _fhirAudience },

                // Support Microsoft Entra ID V1 and V2 endpoints.
                ValidIssuers = validIssuers,
                IssuerSigningKeys = config.SigningKeys,
            };

            SecurityToken securityToken;
            var claimsPrincipal = tokenValidator.ValidateToken(accessToken, validationParameters, out securityToken);
            return claimsPrincipal;
        }
    }
}
