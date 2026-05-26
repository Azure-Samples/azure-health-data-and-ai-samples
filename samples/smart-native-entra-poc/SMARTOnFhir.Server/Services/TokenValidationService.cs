using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SMARTOnFhir.Server.Configuration;

namespace SMARTOnFhir.Server.Services
{
    public class TokenValidationService
    {
        private readonly SmartConfig _config;
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

        public TokenValidationService(SmartConfig config)
        {
            _config = config;

            // Cache OIDC config — created once, reused for all validations
            var authority = config.AuthorityUrl.TrimEnd('/');
            if (authority.EndsWith("/oauth2/v2.0", StringComparison.OrdinalIgnoreCase))
            {
                authority = authority[..^"/oauth2/v2.0".Length];
            }

            _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{authority}/v2.0/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever());
        }

        public async Task<ClaimsPrincipal> ValidateAccessTokenAsync(string accessToken)
        {
            var config = await _configManager.GetConfigurationAsync();

            var validationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidAudiences = new[]
                {
                    _config.ContextAppClientId,
                    _config.FhirResourceAppId,
                    $"api://{_config.ContextAppClientId}",
                    $"api://{_config.FhirResourceAppId}",
                    _config.FhirAudience
                },
                ValidIssuers = new[]
                {
                    config.Issuer,
                    $"https://login.windows.net/{_config.TenantId}/",
                    $"https://login.microsoft.com/{_config.TenantId}/",
                    $"https://sts.windows.net/{_config.TenantId}/",
                },
                IssuerSigningKeys = config.SigningKeys,
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(accessToken, validationParameters, out _);
        }

        public string GetUserId(ClaimsPrincipal principal)
        {
            return principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                ?? principal.FindFirst("oid")?.Value
                ?? throw new UnauthorizedAccessException("No user identifier found in token");
        }
    }
}
