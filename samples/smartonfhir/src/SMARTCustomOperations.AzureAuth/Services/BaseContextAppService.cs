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

#pragma warning disable CA1002 // Do not expose generic lists

namespace SMARTCustomOperations.AzureAuth.Services
{
    public class BaseContextAppService
    {
        private readonly bool _debug;
        private readonly string _contextAppClientId;
        private readonly string _fhirAudience;
        private readonly string _tenantId;
        private readonly string _fhirResourceAppId;
        private readonly string _smartonfhir_with_b2c;
        private readonly string _b2c_authority_url

        public BaseContextAppService(AzureAuthOperationsConfig configuration, ILogger<BaseContextAppService> logger)
        {
            _debug = configuration.Debug;
            _contextAppClientId = configuration.ContextAppClientId!;
            _fhirAudience = configuration.FhirAudience!;
            _tenantId = configuration.TenantId!;
            _fhirResourceAppId = configuration.Fhir_Resource_AppId;
            _smartonfhir_with_b2c = configuration.SmartonFhir_with_B2C;
            _b2c_authority_url= configuration.B2C_Authority_URL;
            _b2c_tenant_name = configuration.B2C_Tenant_Name;
        }

        // https://github.com/Azure-Samples/ms-identity-dotnet-webapi-azurefunctions/blob/master/Function/BootLoader.cs
        public async Task<ClaimsPrincipal> ValidateContextAccessTokenAsync(string accessToken)
        {
            var authority = _smartonfhir_with_b2c ? $"{_b2c_authority_url}/v2.0" : $"https://login.microsoftonline.com/{_tenantId}/v2.0";
            //var authority = $"https://login.microsoftonline.com/{_tenantId}/v2.0";
            //var authority = $"https://fhirb2ctenantdemo.b2clogin.com/fhirb2ctenantdemo.onmicrosoft.com/B2C_1_signupsignin1/v2.0";
            var result = _smartonfhir_with_b2c ? $"{_b2c_tenant_name}.b2clogin" : "login.microsoftonline"
            var validIssuers = new List<string>()
            {
                $"https://{result}.com/{_tenantId}/",
                $"https://{result}.com/{_tenantId}/v2.0/",
                $"https://login.windows.net/{_tenantId}/",
                $"https://login.microsoft.com/{_tenantId}/",
                $"https://sts.windows.net/{_tenantId}/",
            };
            //var validIssuers = new List<string>()
            //{
            //    $"https://login.microsoftonline.com/{_tenantId}/",
            //    $"https://login.microsoftonline.com/{_tenantId}/v2.0",
            //    $"https://login.windows.net/{_tenantId}/",
            //    $"https://login.microsoft.com/{_tenantId}/",
            //    $"https://sts.windows.net/{_tenantId}/",
            //};

            // Debugging purposes only, set this to false for production
            if (_debug)
            {
                Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            }

            ConfigurationManager<OpenIdConnectConfiguration> configManager =
                new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{authority}/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever());

            OpenIdConnectConfiguration config = await configManager.GetConfigurationAsync();

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

                // Support Azure AD V1 and V2 endpoints.
                ValidIssuers = validIssuers,
                IssuerSigningKeys = config.SigningKeys,
            };

            SecurityToken securityToken;
            var claimsPrincipal = tokenValidator.ValidateToken(accessToken, validationParameters, out securityToken);
            return claimsPrincipal;
        }
    }
}
