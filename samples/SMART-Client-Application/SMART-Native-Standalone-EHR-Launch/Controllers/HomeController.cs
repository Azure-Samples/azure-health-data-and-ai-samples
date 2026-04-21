using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartOnFhirDemo.Infrastructure;
using SmartOnFhirDemo.Models;

namespace SmartOnFhirDemo.Controllers;

public class HomeController : Controller
{
    private readonly SmartConfigService _smartConfigService;
    private readonly IConfiguration _configuration;

    public HomeController(SmartConfigService smartConfigService, IConfiguration configuration)
    {
        _smartConfigService = smartConfigService;
        _configuration = configuration;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // ── EHR deep-link detection ────────────────────────────────────────────────
        // A real EHR will open the app as:
        //   https://your-app/?iss=https://ehr.example.org/fhir&launch=<token>
        // Detect those params here and forward into the standard /login flow so that
        // the browser does not have to manually pick "EHR" on the UI.
        var issParam    = HttpContext.Request.Query["iss"].ToString();
        var launchParam = HttpContext.Request.Query["launch"].ToString();

        if (!string.IsNullOrWhiteSpace(issParam) && !string.IsNullOrWhiteSpace(launchParam))
        {
            return RedirectToAction("Login", "Smart", new
            {
                launchType = LaunchType.Ehr,
                iss        = issParam,
                launch     = launchParam
            });
        }
        // ── end EHR deep-link detection ───────────────────────────────────────────

        var session = HttpContext.Session;

        var accessToken    = session.GetString(SessionKeys.AccessToken);
        var refreshToken   = session.GetString(SessionKeys.RefreshToken);
        var lastError      = session.GetString(SessionKeys.LastError);
        var lastSuccess    = session.GetString(SessionKeys.LastSuccess);
        var fhirResultJson = session.GetString(SessionKeys.LastFhirResourceJson);
        var fhirResultType = session.GetString(SessionKeys.LastFhirResourceType);
        var launchType     = session.GetString(SessionKeys.LaunchType) ?? LaunchType.Standalone;
        var sessionIss           = session.GetString(SessionKeys.Iss);
        var rawTokenResponseJson = session.GetString(SessionKeys.LastTokenResponseJson);
        var backendM2mTokenJson  = session.GetString(SessionKeys.BackendTokenResponseJson);
        var backendAccessToken   = session.GetString(SessionKeys.BackendAccessToken);

        string? scopes   = null;
        string? fhirUser = null;

        if (!string.IsNullOrEmpty(accessToken))
        {
            try
            {
                var handler   = new JwtSecurityTokenHandler();
                var jwt       = handler.ReadJwtToken(accessToken);
                var scpValues = jwt.Claims.Where(c => c.Type == "scp").Select(c => c.Value).ToList();
                scopes   = scpValues.Count > 0
                    ? string.Join(" ", scpValues)
                    : jwt.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
                fhirUser = jwt.Claims.FirstOrDefault(c => c.Type == "fhirUser")?.Value;
            }
            catch
            {
                // Ignore decode errors for demo purposes.
            }
        }

        // For EHR launch, show the SMART config from the ISS server;
        // for standalone use the configured FhirBaseUrl;
        // for backend use BackendServices:FhirBaseUrl (the actual FHIR server).
        var smartConfigIss = (launchType == LaunchType.Backend)
            ? _configuration["BackendServices:FhirBaseUrl"]
            : sessionIss;
        var smartConfigRawJson = await _smartConfigService.GetRawConfigurationJsonAsync(cancellationToken, issUrl: smartConfigIss);

        var showFhirModal = HttpContext.Request.Query["show"].ToString();
        if (string.IsNullOrEmpty(showFhirModal)) showFhirModal = null;

        var model = new HomeViewModel
        {
            IsAuthenticated    = !string.IsNullOrEmpty(accessToken),
            AccessToken        = accessToken,
            RefreshToken       = refreshToken,
            Scopes             = scopes,
            FhirUser           = fhirUser,
            TokenResponseJson  = FormatTokenResponseForDisplay(rawTokenResponseJson),
            LastError          = lastError,
            LastSuccess        = lastSuccess,
            FhirResultJson     = fhirResultJson,
            FhirResultType     = fhirResultType,
            SelectedLaunchType = launchType,
            SmartConfigRawJson     = smartConfigRawJson,
            ShowFhirModal          = showFhirModal,
            EhrSimulatorIssDefault = _configuration["SmartOnFhir:FhirBaseUrl"],
            HasUserContext         = !string.IsNullOrEmpty(session.GetString(SessionKeys.UserContextToken)),
            BackendM2mTokenJson    = backendM2mTokenJson,
            HasBackendToken        = !string.IsNullOrEmpty(backendAccessToken)
        };

        // Clear transient messages after displaying once
        session.Remove(SessionKeys.LastError);
        session.Remove(SessionKeys.LastSuccess);

        return View(model);
    }

    private static string? FormatTokenResponseForDisplay(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return rawJson;
        }
    }
}
