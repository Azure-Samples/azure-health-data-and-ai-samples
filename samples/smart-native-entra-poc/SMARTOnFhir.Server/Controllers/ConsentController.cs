using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using SMARTOnFhir.Server.Configuration;
using SMARTOnFhir.Server.Models;
using SMARTOnFhir.Server.Services;

namespace SMARTOnFhir.Server.Controllers
{
    [ApiController]
    public class ConsentController : ControllerBase
    {
        private readonly SmartConfig _config;
        private readonly ILogger<ConsentController> _logger;
        private readonly GraphServiceClient _graphClient;
        private readonly TokenValidationService _tokenValidator;

        public ConsentController(
            IOptions<SmartConfig> config,
            ILogger<ConsentController> logger,
            GraphServiceClient graphClient,
            TokenValidationService tokenValidator)
        {
            _config = config.Value;
            _logger = logger;
            _graphClient = graphClient;
            _tokenValidator = tokenValidator;
        }

        [HttpGet("/auth/appConsentInfo")]
        public async Task<IActionResult> GetConsentInfo(
            [FromQuery] string client_id,
            [FromQuery] string? scope)
        {
            // Validate the caller's access token
            var authHeader = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("No Bearer token provided");
            }

            var token = authHeader["Bearer ".Length..];
            System.Security.Claims.ClaimsPrincipal principal;
            try
            {
                principal = await _tokenValidator.ValidateAccessTokenAsync(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token validation failed");
                return Unauthorized($"Token validation failed: {ex.Message}");
            }

            var userId = _tokenValidator.GetUserId(principal);

            if (string.IsNullOrEmpty(client_id))
            {
                return BadRequest("client_id is required");
            }

            // Convert SMART scopes (with /) to Entra dot format (with .) before matching against SP scopes.
            // Use empty audience — the consent UI only needs the bare scope names, not audience-prefixed ones.
            var entraScopes = SmartScopeMapper.ToEntraFormat(
                (scope ?? "").Replace("+", " "), string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            try
            {
                var info = await BuildConsentInfo(client_id, userId, entraScopes);
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching consent info from Graph");
                return StatusCode(500, "Error fetching consent information");
            }
        }

        [HttpPost("/auth/appConsentInfo")]
        public async Task<IActionResult> SaveConsentInfo()
        {
            var principal = await ValidateCallerToken();
            if (principal == null) return Unauthorized();

            var userId = _tokenValidator.GetUserId(principal);
            var body = await new StreamReader(Request.Body).ReadToEndAsync();
            _logger.LogInformation("SaveConsentInfo received body: {Body}", body);
            var consentInfo = System.Text.Json.JsonSerializer.Deserialize<AppConsentInfo>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _logger.LogInformation("Deserialized: ApplicationId={AppId}, Scopes={ScopeCount}",
                consentInfo?.ApplicationId, consentInfo?.Scopes?.Count);

            if (consentInfo?.ApplicationId == null || consentInfo?.Scopes == null)
            {
                return BadRequest("Invalid consent info");
            }

            try
            {
                await PersistConsent(consentInfo, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving consent info");
                return StatusCode(500, "Error saving consent information");
            }
        }

        [HttpOptions("/auth/appConsentInfo")]
        public IActionResult AppConsentInfoOptions()
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            Response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type";
            return Ok();
        }

        private async Task<System.Security.Claims.ClaimsPrincipal?> ValidateCallerToken()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return null;
            }

            var token = authHeader["Bearer ".Length..];
            try
            {
                return await _tokenValidator.ValidateAccessTokenAsync(token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return null;
            }
        }

        private async Task<AppConsentInfo> BuildConsentInfo(string clientId, string userId, string[] requestedScopes)
        {
            // Parallel: fetch app registration AND requesting service principal at the same time
            var appTask = _graphClient.Applications.GetAsync(rq =>
            {
                rq.QueryParameters.Filter = $"appId eq '{clientId}'";
            });

            var reqSpTask = _graphClient.ServicePrincipals.GetAsync(rq =>
            {
                rq.QueryParameters.Filter = $"appId eq '{clientId}'";
            });

            await Task.WhenAll(appTask, reqSpTask);

            var app = appTask.Result?.Value?.SingleOrDefault()
                ?? throw new ArgumentException($"Application not found: {clientId}");
            var reqSp = reqSpTask.Result?.Value?.SingleOrDefault()
                ?? throw new ArgumentException($"Service principal not found: {clientId}");

            // Parallel: fetch resource service principals AND user permissions at the same time
            var resourceAppIds = app.RequiredResourceAccess?.Select(x => x.ResourceAppId!).Distinct() ?? Enumerable.Empty<string>();

            var resourceSpTasks = resourceAppIds.Select(resAppId =>
                _graphClient.ServicePrincipals.GetAsync(rq =>
                {
                    rq.QueryParameters.Filter = $"appId eq '{resAppId}'";
                }));

            var grantsTask = _graphClient.Oauth2PermissionGrants.GetAsync(rq =>
            {
                rq.QueryParameters.Filter = $"clientId eq '{reqSp.Id}' and consentType eq 'Principal' and principalId eq '{userId}'";
            });

            var allTasks = resourceSpTasks.Cast<Task>().Append(grantsTask);
            await Task.WhenAll(allTasks);

            var resourceSps = new Dictionary<string, ServicePrincipal>();
            foreach (var (resAppId, task) in resourceAppIds.Zip(resourceSpTasks))
            {
                var result = await task;
                if (result?.Value?.Any() == true)
                {
                    resourceSps[resAppId] = result.Value.First();
                }
            }

            var permissions = grantsTask.Result?.Value ?? new List<OAuth2PermissionGrant>();

            // Build consent info
            var info = new AppConsentInfo
            {
                ApplicationId = app.AppId,
                ApplicationName = app.DisplayName,
                ApplicationDescription = app.Description,
                ApplicationUrl = app.Info?.MarketingUrl ?? string.Empty
            };

            var existingScopes = string.Join(" ", permissions.Select(x => x.Scope)).Split(" ", StringSplitOptions.RemoveEmptyEntries);

            // Normalize SMART scopes from URL format (/) to Entra format (.)
            // e.g. "patient/Patient.rs" → "patient.Patient.rs", "launch/patient" → "launch.patient"
            // This matches the original AppConsentInfoInputFilter behavior
            var normalizedRequested = requestedScopes
                .Select(s => s.Replace("/", ".").Replace(".*", ".all"))
                .Where(s => !string.Equals(s, "openid", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(s, "offline_access", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(s, "fhirUser", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var allRequestedScopes = existingScopes.Union(normalizedRequested).Distinct();

            // Transform scopes and add granular variants
            var graphAppId = "00000003-0000-0000-c000-000000000000";
            var allSpScopes = resourceSps.Values
                .Where(sp => sp.AppId != graphAppId && sp.Oauth2PermissionScopes != null)
                .SelectMany(sp => sp.Oauth2PermissionScopes!)
                .ToList();

            var expandedScopes = new HashSet<string>(allRequestedScopes, StringComparer.OrdinalIgnoreCase);
            foreach (var reqScope in allRequestedScopes)
            {
                foreach (var spScope in allSpScopes)
                {
                    var spRoot = GetScopeRoot(spScope.Value!);
                    if (spRoot.Equals(reqScope, StringComparison.OrdinalIgnoreCase))
                    {
                        expandedScopes.Add(spScope.Value!);
                    }
                }
            }

            // Get app scope IDs
            var appScopeIds = app.RequiredResourceAccess?
                .SelectMany(r => r.ResourceAccess?.Where(a => a.Type == "Scope").Select(a => (Guid)a.Id!) ?? Enumerable.Empty<Guid>())
                .ToList() ?? new List<Guid>();

            foreach (var scope in expandedScopes)
            {
                var matchingSp = resourceSps.Values
                    .FirstOrDefault(sp => sp.Oauth2PermissionScopes?.Any(y => appScopeIds.Contains((Guid)y.Id!) && y.Value == scope) == true);
                var scopeInfo = matchingSp?.Oauth2PermissionScopes?.FirstOrDefault(x => x.Value == scope);
                var consentRecord = permissions.FirstOrDefault(x => x.ResourceId == matchingSp?.Id
                    && x.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(s => s.Equals(scope, StringComparison.OrdinalIgnoreCase)) == true);

                if (matchingSp != null && scopeInfo?.Id != null)
                {
                    info.Scopes.Add(new AppConsentScope
                    {
                        Name = scopeInfo.Value,
                        Id = scopeInfo.Id.ToString(),
                        ResourceId = matchingSp.Id,
                        Consented = consentRecord != null,
                        ConsentId = consentRecord?.Id,
                        UserDescription = scopeInfo.UserConsentDescription
                    });
                }
            }

            return info;
        }

        private async Task PersistConsent(AppConsentInfo consentInfo, string userId)
        {
            foreach (var resourceId in consentInfo.Scopes.Select(x => x.ResourceId).Distinct())
            {
                var resourceScopes = consentInfo.Scopes.Where(x => x.ResourceId == resourceId).ToList();
                var scopeString = string.Join(" ", resourceScopes.Where(x => x.Consented).Select(x => x.Name));

                if (resourceScopes.Any(x => x.ConsentId != null))
                {
                    var consentId = resourceScopes.First(x => x.ConsentId != null).ConsentId!;
                    var existing = await _graphClient.Oauth2PermissionGrants[consentId].GetAsync();
                    if (existing != null)
                    {
                        var existingScopes = existing.Scope?.Split(' ') ?? Array.Empty<string>();
                        var newScopes = scopeString.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        // If any scopes were removed, DELETE the entire grant (matches original behavior).
                        // The subsequent Entra consent prompt will re-create it with only the user-selected scopes.
                        if (existingScopes.Except(newScopes).Any())
                        {
                            _logger.LogInformation("Deleting OAuth2PermissionGrant {ConsentId} for scope removal", consentId);
                            await _graphClient.Oauth2PermissionGrants[consentId].DeleteAsync();
                        }
                    }
                }
            }
        }

        private static string GetScopeRoot(string scope)
        {
            var idx = scope.IndexOf('?');
            return idx < 0 ? scope : scope[..idx];
        }
    }
}
