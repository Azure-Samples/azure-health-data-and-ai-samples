// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Services;
using SMARTCustomOperations.AzureAuth.Strategies;

namespace SMARTCustomOperations.AzureAuth
{
    /// <summary>
    /// Catch-all FHIR proxy that forwards requests to the configured Azure FHIR Service.
    /// This allows clients to use the gateway as their single FHIR endpoint — the client
    /// never needs to know the real FHIR Service URL.
    ///
    /// Proxied: /metadata, /Patient, /Observation, etc.
    /// Not proxied: /api/* routes (handled by other functions with higher-priority routes).
    /// </summary>
    public class FhirProxyFunction
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _config;
        private readonly FhirSmartConfigService _smartConfigService;
        private readonly IIdpStrategy _idpStrategy;
        private readonly HttpClient _httpClient;
        private readonly IBulkExportService? _bulkExportService;

        public FhirProxyFunction(
            ILogger<FhirProxyFunction> logger,
            AzureAuthOperationsConfig config,
            FhirSmartConfigService smartConfigService,
            IIdpStrategy idpStrategy,
            IHttpClientFactory httpClientFactory,
            IBulkExportService? bulkExportService = null)
        {
            _logger = logger;
            _config = config;
            _smartConfigService = smartConfigService;
            _idpStrategy = idpStrategy;
            _httpClient = httpClientFactory.CreateClient("FhirProxy");
            _bulkExportService = bulkExportService;
        }

        [Function("FhirProxy")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "patch", "delete", "options", Route = @"{*path:regex(^(?!api/).*)}")] HttpRequestData req,
            string path)
        {
            var normalizedPath = (path ?? string.Empty).Trim('/');

            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                var preflight = req.CreateResponse(HttpStatusCode.NoContent);
                AddCorsHeaders(req, preflight);
                return preflight;
            }

            if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedPath, ".well-known/smart-configuration", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Handling SMART well-known from FhirProxy route.");
                return await BuildSmartConfigurationResponseAsync(req);
            }

            // When bulk export is enabled, augment /metadata so the CapabilityStatement declares the
            // Group $export operation (required by Inferno g10 test 7.2.02). AHDS does not advertise
            // it natively, and this sample has no APIM layer to rewrite it.
            if (_bulkExportService is not null
                && req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedPath, "metadata", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Augmenting CapabilityStatement with bulk export operations.");
                return await BuildCapabilityStatementResponseAsync(req);
            }

            // SMART Backend Services bulk data export (Group $export kickoff, status, delete).
            // Only intercepted when export storage is configured (Entra mode); otherwise the
            // request falls through to the generic FHIR proxy below.
            if (_bulkExportService is not null)
            {
                if (_bulkExportService.IsExportFileRequest("/" + normalizedPath))
                {
                    return await HandleExportFileAsync(req, normalizedPath);
                }

                var exportType = _bulkExportService.Classify(req.Method, "/" + normalizedPath);
                if (exportType != BulkExportRequestType.None)
                {
                    return await HandleBulkExportAsync(req, normalizedPath);
                }
            }

            _logger.LogInformation("FHIR proxy: {Method} /{Path}", req.Method, path);

            var fhirBaseUrl = _config.FhirServerUrl?.TrimEnd('/');
            if (string.IsNullOrEmpty(fhirBaseUrl))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("FHIR server URL is not configured.");
                return errorResponse;
            }

            // Build the target URL: FHIR base + path + query string
            var targetUrl = $"{fhirBaseUrl}/{path}{req.Url.Query}";

            // Build the outgoing request
            var proxyRequest = new HttpRequestMessage(new HttpMethod(req.Method), targetUrl);

            // Copy the Authorization header (Bearer token) to the FHIR request
            if (req.Headers.TryGetValues("Authorization", out var authValues))
            {
                proxyRequest.Headers.TryAddWithoutValidation("Authorization", authValues.First());
            }

            // Copy Accept header
            if (req.Headers.TryGetValues("Accept", out var acceptValues))
            {
                proxyRequest.Headers.Accept.Clear();
                foreach (var accept in acceptValues)
                {
                    proxyRequest.Headers.Accept.TryParseAdd(accept);
                }
            }

            // Copy request body for POST/PUT/PATCH
            if (req.Body != null && req.Body.Length > 0 && !HttpMethod.Get.Method.Equals(req.Method, StringComparison.OrdinalIgnoreCase))
            {
                req.Body.Position = 0;
                proxyRequest.Content = new StreamContent(req.Body);

                if (req.Headers.TryGetValues("Content-Type", out var contentTypeValues))
                {
                    proxyRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentTypeValues.First());
                }
            }

            // Copy Prefer header (used for FHIR return preferences)
            if (req.Headers.TryGetValues("Prefer", out var preferValues))
            {
                proxyRequest.Headers.TryAddWithoutValidation("Prefer", preferValues.First());
            }

            // Copy If-Match / If-None-Match for conditional operations
            if (req.Headers.TryGetValues("If-Match", out var ifMatchValues))
            {
                proxyRequest.Headers.TryAddWithoutValidation("If-Match", ifMatchValues.First());
            }

            if (req.Headers.TryGetValues("If-None-Match", out var ifNoneMatchValues))
            {
                proxyRequest.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatchValues.First());
            }

            // Forward the request to FHIR Service
            HttpResponseMessage fhirResponse;
            try
            {
                fhirResponse = await _httpClient.SendAsync(proxyRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to proxy request to FHIR Service: {TargetUrl}", targetUrl);
                var errorResponse = req.CreateResponse(HttpStatusCode.BadGateway);
                await errorResponse.WriteStringAsync("Failed to reach FHIR Service.");
                return errorResponse;
            }

            // Build the response back to the client
            var response = req.CreateResponse(fhirResponse.StatusCode);

            // Add CORS headers so browser-based SMART clients (and Inferno's CORS conformance
            // checks on /metadata) can consume the response cross-origin.
            AddCorsHeaders(req, response);

            // Copy FHIR response headers
            if (fhirResponse.Content.Headers.ContentType != null)
            {
                response.Headers.Add("Content-Type", fhirResponse.Content.Headers.ContentType.ToString());
            }

            if (fhirResponse.Headers.ETag != null)
            {
                response.Headers.Add("ETag", fhirResponse.Headers.ETag.ToString());
            }

            if (fhirResponse.Headers.Location != null)
            {
                response.Headers.Add("Location", fhirResponse.Headers.Location.ToString());
            }

            // Copy the response body
            var responseBody = await fhirResponse.Content.ReadAsStringAsync();
            await response.WriteStringAsync(responseBody);

            return response;
        }

        private async Task<HttpResponseData> HandleExportFileAsync(HttpRequestData req, string normalizedPath)
        {
            string? Header(string name) => req.Headers.TryGetValues(name, out var v) ? v.FirstOrDefault() : null;

            var fileResult = await _bulkExportService!.GetExportFileAsync("/" + normalizedPath, Header("Authorization"));

            var response = req.CreateResponse(fileResult.StatusCode);
            AddCorsHeaders(req, response);

            if (fileResult.Content is not null)
            {
                response.Headers.Add("Content-Type", "application/fhir+ndjson");
                response.Headers.Add("Cache-Control", "private, no-store");
                response.Headers.Add("X-Content-Type-Options", "nosniff");

                // Stream the blob directly to the response to avoid buffering large export files.
                await fileResult.Content.CopyToAsync(response.Body);
                await fileResult.Content.DisposeAsync();
                return response;
            }

            if (fileResult.ErrorBody is not null)
            {
                response.Headers.Add("Content-Type", "application/fhir+json");
                await response.WriteStringAsync(fileResult.ErrorBody);
            }

            return response;
        }

        private async Task<HttpResponseData> HandleBulkExportAsync(HttpRequestData req, string normalizedPath)
        {
            string? Header(string name) => req.Headers.TryGetValues(name, out var v) ? v.FirstOrDefault() : null;

            var exportRequest = new BulkExportRequest
            {
                Method = req.Method,
                LocalPath = "/" + normalizedPath,
                Query = req.Url.Query,
                AuthorizationHeader = Header("Authorization"),
                AcceptHeader = Header("Accept"),
                PreferHeader = Header("Prefer"),
                GatewayBaseUrl = $"{req.Url.Scheme}://{req.Url.Authority}",
            };

            var result = await _bulkExportService!.HandleAsync(exportRequest);

            var response = req.CreateResponse(result.StatusCode);
            AddCorsHeaders(req, response);

            foreach (var header in result.Headers)
            {
                response.Headers.Add(header.Key, header.Value);
            }

            if (result.Body is not null)
            {
                response.Headers.Add("Content-Type", result.ContentType);
                await response.WriteStringAsync(result.Body);
            }

            return response;
        }

        private async Task<HttpResponseData> BuildCapabilityStatementResponseAsync(HttpRequestData req)
        {
            var fhirBaseUrl = _config.FhirServerUrl?.TrimEnd('/');
            if (string.IsNullOrEmpty(fhirBaseUrl))
            {
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync("FHIR server URL is not configured.");
                return err;
            }

            // Stage 1: fetch the upstream CapabilityStatement.
            string body;
            HttpStatusCode upstreamStatus;
            bool upstreamOk;
            try
            {
                using var upstreamRequest = new HttpRequestMessage(HttpMethod.Get, $"{fhirBaseUrl}/metadata");
                upstreamRequest.Headers.TryAddWithoutValidation("Accept", "application/fhir+json");
                if (req.Headers.TryGetValues("Authorization", out var authValues))
                {
                    upstreamRequest.Headers.TryAddWithoutValidation("Authorization", authValues.First());
                }

                using var upstreamResponse = await _httpClient.SendAsync(upstreamRequest);
                body = await upstreamResponse.Content.ReadAsStringAsync();
                upstreamStatus = upstreamResponse.StatusCode;
                upstreamOk = upstreamResponse.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch CapabilityStatement from FHIR Service at {FhirBaseUrl}/metadata.", fhirBaseUrl);
                var errorResponse = req.CreateResponse(HttpStatusCode.BadGateway);
                await errorResponse.WriteStringAsync(_config.Debug
                    ? $"Failed to reach FHIR Service /metadata: {ex.Message}"
                    : "Failed to retrieve CapabilityStatement from FHIR Service.");
                return errorResponse;
            }

            if (!upstreamOk)
            {
                var passthrough = req.CreateResponse(upstreamStatus);
                AddCorsHeaders(req, passthrough);
                passthrough.Headers.Add("Content-Type", "application/fhir+json");
                await passthrough.WriteStringAsync(body);
                return passthrough;
            }

            // Stage 2: augment. Never hard-fail here — if augmentation trips, log and serve the
            // original CapabilityStatement so /metadata still works.
            string payload;
            string? augmentError = null;
            try
            {
                payload = AddBulkExportOperations(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to augment CapabilityStatement with bulk export operations; serving upstream copy.");
                payload = body;
                augmentError = ex.Message;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            AddCorsHeaders(req, response);
            response.Headers.Add("Content-Type", "application/fhir+json");
            if (augmentError is not null && _config.Debug)
            {
                response.Headers.Add("X-Augment-Error", augmentError.Replace('\n', ' ').Replace('\r', ' '));
            }

            await response.WriteStringAsync(payload);
            return response;
        }

        // Injects the Bulk Data export operations into the CapabilityStatement so Inferno's g10
        // Multi-Patient tests can discover them. Adds the Group-level $export operation (required by
        // test 7.2.02) plus the Patient- and system-level export operations for completeness.
        private static string AddBulkExportOperations(string capabilityStatementJson)
        {
            var root = JsonNode.Parse(capabilityStatementJson);
            if (root is not JsonObject capability || capability["rest"] is not JsonArray restArray || restArray.Count == 0)
            {
                return capabilityStatementJson;
            }

            // Use the first server-mode rest entry (fall back to the first entry).
            var serverRest = restArray
                .OfType<JsonObject>()
                .FirstOrDefault(r => string.Equals(r["mode"]?.GetValue<string>(), "server", StringComparison.OrdinalIgnoreCase))
                ?? restArray[0] as JsonObject;

            if (serverRest is null)
            {
                return capabilityStatementJson;
            }

            // Declare conformance to the Bulk Data IG via the top-level instantiates element
            // (SHOULD per the IG; Inferno warns when absent).
            const string bulkDataCanonical = "http://hl7.org/fhir/uv/bulkdata/CapabilityStatement/bulk-data";
            if (capability["instantiates"] is JsonArray existingInstantiates)
            {
                var alreadyDeclared = existingInstantiates
                    .Any(n => string.Equals(n?.ToString(), bulkDataCanonical, StringComparison.Ordinal));
                if (!alreadyDeclared)
                {
                    existingInstantiates.Add(JsonValue.Create(bulkDataCanonical));
                }
            }
            else
            {
                capability["instantiates"] = new JsonArray(JsonValue.Create(bulkDataCanonical));
            }

            // System-level export: an operation on the server-level rest entry.
            var restOperations = serverRest["operation"] as JsonArray;
            if (restOperations is null)
            {
                restOperations = new JsonArray();
                serverRest["operation"] = restOperations;
            }

            EnsureOperation(restOperations, "export", "http://hl7.org/fhir/uv/bulkdata/OperationDefinition/export");

            // Resource-level export operations (Group and Patient).
            var resources = serverRest["resource"] as JsonArray;
            if (resources is null)
            {
                resources = new JsonArray();
                serverRest["resource"] = resources;
            }

            var groupResource = EnsureResource(resources, "Group");
            var groupOperations = groupResource["operation"] as JsonArray;
            if (groupOperations is null)
            {
                groupOperations = new JsonArray();
                groupResource["operation"] = groupOperations;
            }

            EnsureOperation(groupOperations, "export", "http://hl7.org/fhir/uv/bulkdata/OperationDefinition/group-export");

            var patientResource = EnsureResource(resources, "Patient");
            var patientOperations = patientResource["operation"] as JsonArray;
            if (patientOperations is null)
            {
                patientOperations = new JsonArray();
                patientResource["operation"] = patientOperations;
            }

            EnsureOperation(patientOperations, "export", "http://hl7.org/fhir/uv/bulkdata/OperationDefinition/patient-export");

            return capability.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }

        private static JsonObject EnsureResource(JsonArray resources, string resourceType)
        {
            var existing = resources
                .OfType<JsonObject>()
                .FirstOrDefault(r => string.Equals(r["type"]?.GetValue<string>(), resourceType, StringComparison.Ordinal));

            if (existing is not null)
            {
                return existing;
            }

            var created = new JsonObject { ["type"] = resourceType };
            resources.Add(created);
            return created;
        }

        private static void EnsureOperation(JsonArray operations, string name, string definition)
        {
            var alreadyPresent = operations
                .OfType<JsonObject>()
                .Any(o => string.Equals(o["definition"]?.GetValue<string>(), definition, StringComparison.Ordinal));

            if (!alreadyPresent)
            {
                operations.Add(new JsonObject
                {
                    ["name"] = name,
                    ["definition"] = definition,
                });
            }
        }

        private async Task<HttpResponseData> BuildSmartConfigurationResponseAsync(HttpRequestData req)
        {
            try
            {
                var smartConfig = await _smartConfigService.GetSmartConfigurationAsync();
                var gatewayBaseUrl = $"{req.Url.Scheme}://{req.Url.Authority}";
                var modifiedConfig = new Dictionary<string, JsonElement>(smartConfig)
                {
                    ["token_endpoint"] = JsonSerializer.SerializeToElement($"{gatewayBaseUrl}/api/token")
                };

                if (_idpStrategy.ProvidesAuthorizeProxy)
                {
                    modifiedConfig["authorization_endpoint"] = JsonSerializer.SerializeToElement($"{gatewayBaseUrl}/api/authorize");
                }

                // Merge the upstream capabilities list with the SMART v2 capabilities that this
                // gateway actually implements. Required by Inferno g10 test 1.8.05 (missing
                // context-standalone-patient, permission-v1 in the native FHIR response).
                modifiedConfig["capabilities"] = JsonSerializer.SerializeToElement(
                    BuildCapabilities(smartConfig));

                // SMART v2 Backend Services discovery (FHIR Bulk Data / Inferno g10 Multi-Patient
                // Authorization and API test). When this gateway validates asymmetric client
                // assertions itself, guarantee the token endpoint advertises private_key_jwt with
                // RS384/ES384 and the client_credentials grant, regardless of what the upstream
                // FHIR metadata returned.
                if (_idpStrategy.SupportsBackendServices)
                {
                    modifiedConfig["token_endpoint_auth_methods_supported"] = JsonSerializer.SerializeToElement(
                        new[] { "client_secret_basic", "private_key_jwt" });
                    modifiedConfig["token_endpoint_auth_signing_alg_values_supported"] = JsonSerializer.SerializeToElement(
                        new[] { "RS384", "ES384" });
                    modifiedConfig["grant_types_supported"] = JsonSerializer.SerializeToElement(
                        BuildGrantTypesSupported(smartConfig));
                    modifiedConfig["scopes_supported"] = JsonSerializer.SerializeToElement(
                        BuildScopesSupported(smartConfig));
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Cache-Control", "public, max-age=3600");
                AddCorsHeaders(req, response);

                var jsonOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = true
                };

                await response.WriteStringAsync(JsonSerializer.Serialize(modifiedConfig, jsonOptions));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build SMART well-known response in FhirProxy.");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadGateway);
                await errorResponse.WriteStringAsync("Failed to retrieve SMART configuration from FHIR Service.");
                return errorResponse;
            }
        }

        // Capabilities this gateway supports end-to-end, per SMART App Launch 2.x.
        // See http://hl7.org/fhir/smart-app-launch/conformance.html
        private static readonly string[] GatewayCapabilities =
        {
            "launch-ehr",
            "launch-standalone",
            "client-public",
            "client-confidential-symmetric",
            "client-confidential-asymmetric",
            "sso-openid-connect",
            "context-ehr-patient",
            "context-ehr-encounter",
            "context-standalone-patient",
            "context-standalone-encounter",
            "permission-offline",
            "permission-patient",
            "permission-user",
            "permission-v1",
            "permission-v2",
            "authorize-post",
        };

        private static List<string> BuildCapabilities(IReadOnlyDictionary<string, JsonElement> upstreamConfig)
        {
            var caps = new HashSet<string>(StringComparer.Ordinal);

            if (upstreamConfig.TryGetValue("capabilities", out var existing)
                && existing.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in existing.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrEmpty(s))
                        {
                            caps.Add(s);
                        }
                    }
                }
            }

            foreach (var c in GatewayCapabilities)
            {
                caps.Add(c);
            }

            return caps.OrderBy(c => c, StringComparer.Ordinal).ToList();
        }

        // Union the upstream grant_types_supported with the grants this gateway brokers:
        // authorization_code (SMART App Launch) and client_credentials (SMART Backend Services).
        private static List<string> BuildGrantTypesSupported(IReadOnlyDictionary<string, JsonElement> upstreamConfig)
        {
            var grants = CollectStringArray(upstreamConfig, "grant_types_supported");
            grants.Add("authorization_code");
            grants.Add("client_credentials");
            return grants.OrderBy(g => g, StringComparer.Ordinal).ToList();
        }

        // Ensure the advertised scopes include the system-level scopes required for Backend
        // Services bulk export, while preserving any scopes the upstream metadata advertised.
        private static List<string> BuildScopesSupported(IReadOnlyDictionary<string, JsonElement> upstreamConfig)
        {
            var scopes = CollectStringArray(upstreamConfig, "scopes_supported");
            scopes.Add("system/*.read");
            scopes.Add("system/*.rs");
            return scopes.OrderBy(s => s, StringComparer.Ordinal).ToList();
        }

        private static HashSet<string> CollectStringArray(IReadOnlyDictionary<string, JsonElement> config, string key)
        {
            var values = new HashSet<string>(StringComparer.Ordinal);

            if (config.TryGetValue(key, out var existing) && existing.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in existing.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrEmpty(s))
                        {
                            values.Add(s);
                        }
                    }
                }
            }

            return values;
        }

        private static void AddCorsHeaders(HttpRequestData req, HttpResponseData response)
        {
            var origin = req.Headers.TryGetValues("Origin", out var originValues)
                ? originValues.FirstOrDefault()
                : null;

            response.Headers.Add("Access-Control-Allow-Origin", string.IsNullOrEmpty(origin) ? "*" : origin);
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, OPTIONS");
            response.Headers.Add(
                "Access-Control-Allow-Headers",
                "Authorization, Content-Type, Accept, Prefer, If-Match, If-None-Match, X-Requested-With");
            response.Headers.Add("Access-Control-Expose-Headers", "Content-Location, Location, ETag");
            response.Headers.Add("Access-Control-Max-Age", "3600");
            response.Headers.Add("Vary", "Origin");
        }
    }
}
