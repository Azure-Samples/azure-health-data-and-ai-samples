using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SMARTOnFhir.Server.Configuration;
using SMARTOnFhir.Server.Models;
using SMARTOnFhir.Server.Services;

namespace SMARTOnFhir.Server.Controllers
{
    [ApiController]
    public class ContextController : ControllerBase
    {
        private readonly SmartConfig _config;
        private readonly ILogger<ContextController> _logger;
        private readonly IMemoryCache _cache;
        private readonly TokenValidationService _tokenValidator;

        public ContextController(
            IOptions<SmartConfig> config,
            ILogger<ContextController> logger,
            IMemoryCache cache,
            TokenValidationService tokenValidator)
        {
            _config = config.Value;
            _logger = logger;
            _cache = cache;
            _tokenValidator = tokenValidator;
        }

        [HttpPost("/auth/context-cache")]
        public async Task<IActionResult> SaveContext()
        {
            // Validate token
            var authHeader = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized();
            }

            var token = authHeader["Bearer ".Length..];
            System.Security.Claims.ClaimsPrincipal userPrincipal;
            try
            {
                userPrincipal = await _tokenValidator.ValidateAccessTokenAsync(token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed for context-cache");
                return Unauthorized();
            }

            string userId;
            try
            {
                userId = _tokenValidator.GetUserId(userPrincipal);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Token did not contain a stable user identifier for context-cache");
                return Unauthorized("Token is missing required user identifier claim");
            }

            // Parse body
            var body = await new StreamReader(Request.Body).ReadToEndAsync();
            LaunchCacheObject? cacheObject;
            try
            {
                cacheObject = JsonSerializer.Deserialize<LaunchCacheObject>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return BadRequest("Invalid request body");
            }

            if (cacheObject?.UserId == null)
            {
                return BadRequest("userId is required");
            }

            // Verify userId matches token
            if (!string.Equals(cacheObject.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("UserId mismatch: body={BodyUserId}, token={TokenUserId}", cacheObject.UserId, userId);
                return Unauthorized("User ID does not match token");
            }

            // Decode launch context and store in memory cache
            Dictionary<string, string>? launchProps = null;
            if (!string.IsNullOrEmpty(cacheObject.Launch))
            {
                try
                {
                    var decoded = Convert.FromBase64String(cacheObject.Launch);
                    var json = System.Text.Encoding.UTF8.GetString(decoded);
                    launchProps = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                }
                catch
                {
                    // Launch may be empty for standalone without EHR context
                    _logger.LogInformation("Could not decode launch context, storing empty");
                }
            }

            _cache.Set($"launch:{userId}", launchProps ?? new Dictionary<string, string>(),
                TimeSpan.FromMinutes(60));

            _logger.LogInformation("Stored launch context for user {UserId}", userId);
            return NoContent();
        }

        [HttpOptions("/auth/context-cache")]
        public IActionResult ContextCacheOptions()
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
            Response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type";
            return Ok();
        }
    }
}
