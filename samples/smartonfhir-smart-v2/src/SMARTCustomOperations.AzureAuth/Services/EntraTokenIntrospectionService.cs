using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using SMARTCustomOperations.AzureAuth.Models;
using System.IdentityModel.Tokens.Jwt;
using SMARTCustomOperations.AzureAuth.Configuration;
using System.Text;

namespace SMARTCustomOperations.AzureAuth.Services
{
    class EntraTokenIntrospectionService : ITokenIntrospectionService
    {
        private readonly ILogger _logger;
        private readonly ContextCacheService _cacheService;
        private readonly string _tenantId;
        private readonly string _fhirAudience;

        public EntraTokenIntrospectionService(AzureAuthOperationsConfig configuration, ILogger<EntraTokenIntrospectionService> logger, ContextCacheService cacheService)
        {
            _cacheService = cacheService;
            _tenantId = configuration.TenantId!;
            _fhirAudience = configuration.FhirAudience!;
            _logger = logger;
        }
        public async Task<TokenIntrospectionResult> IntrospectAsync(string token)
        {
            try
            {
                var authority = $"https://login.microsoftonline.com/{_tenantId}/v2.0";
                var validIssuers = new List<string>()
            {
                $"https://login.microsoftonline.com/{_tenantId}/",
                $"https://login.microsoftonline.com/{_tenantId}/v2.0",
                $"https://login.windows.net/{_tenantId}/",
                $"https://login.microsoft.com/{_tenantId}/",
                $"https://sts.windows.net/{_tenantId}/",
            };

                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);


                ConfigurationManager<OpenIdConnectConfiguration> configManager =
                    new ConfigurationManager<OpenIdConnectConfiguration>(
                        $"{authority}/.well-known/openid-configuration",
                        new OpenIdConnectConfigurationRetriever());

                OpenIdConnectConfiguration config = await configManager.GetConfigurationAsync();

                var validationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudiences = new[] { _fhirAudience },
                    ValidateIssuer = true,
                    ValidIssuers = validIssuers,
                    IssuerSigningKeys = config.SigningKeys,
                    ValidateLifetime = true
                };

                var principal = handler.ValidateToken(token, validationParameters, out _);

                var scopes = principal.Claims.FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/identity/claims/scope")?.Value.Split(' ');
                var roles = principal.Claims.Where(x => x.Type == "roles").Select(x => x.Value);

                StringBuilder transformedScopes = new StringBuilder();

                foreach (var scope in scopes ?? roles)
                {
                    // These must be case-sensitive to perserve scopes like patient/Patient.read.
                    transformedScopes.Append(scope
                        .Replace(_fhirAudience!, string.Empty, StringComparison.InvariantCulture)
                        .TrimStart('/')
                        .Replace("patient.", "patient/", StringComparison.InvariantCulture)
                        .Replace("user.", "user/", StringComparison.InvariantCulture)
                        .Replace("system.", "system/", StringComparison.InvariantCulture)
                        .Replace("launch.", "launch/", StringComparison.InvariantCulture)
                        .Replace("%2f", "/", StringComparison.InvariantCulture)
                        .Replace("all", "*") + " "
                    );
                }

                // Cross verify before commiting
                transformedScopes.Append("openid offline_access");

                var fhirUser = principal.Claims.FirstOrDefault(c => c.Type == "fhirUser")?.Value;
                var patient = fhirUser?.Substring(fhirUser.LastIndexOf('/') + 1);


                if (principal.Claims.FirstOrDefault(c => c.Type == "uti")?.Value is not null)
                {
                    var idTokenClaim = _cacheService.GetClaimCacheObjectAsync(principal.Claims.FirstOrDefault(c => c.Type == "uti")?.Value).Result;
                    
                    if(idTokenClaim is not null)
                    {
                        return new TokenIntrospectionResult
                        {
                            Active = true,
                            Scope = transformedScopes.ToString(),
                            ClientId = principal.Claims.FirstOrDefault(c => c.Type == "appid")?.Value,
                            Patient = patient,
                            Exp = long.Parse(principal.FindFirst("exp")?.Value ?? "0"),
                            Iss = idTokenClaim.Issuer,
                            Sub = idTokenClaim.Subject,
                            FhirUser = fhirUser,
                        };
                    }
                }

                return new TokenIntrospectionResult
                {
                    Active = true,
                    Scope = transformedScopes.ToString(),
                    ClientId = principal.Claims.FirstOrDefault(c => c.Type == "appid")?.Value,
                    Patient = patient,
                    Exp = long.Parse(principal.FindFirst("exp")?.Value ?? "0"),
                    FhirUser = fhirUser,
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token introspection failed.");
                return new TokenIntrospectionResult { Active = false };
            }

        }
    }
}
