// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Services;

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
        private readonly HttpClient _httpClient;

        public FhirProxyFunction(
            ILogger<FhirProxyFunction> logger,
            AzureAuthOperationsConfig config,
            FhirSmartConfigService smartConfigService,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _config = config;
            _smartConfigService = smartConfigService;
            _httpClient = httpClientFactory.CreateClient("FhirProxy");
        }

        [Function("FhirProxy")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "patch", "delete", Route = @"{*path:regex(^(?!api/).*)}")] HttpRequestData req,
            string path)
        {
            var normalizedPath = (path ?? string.Empty).Trim('/');
            if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedPath, ".well-known/smart-configuration", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Handling SMART well-known from FhirProxy route.");
                return await BuildSmartConfigurationResponseAsync(req);
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

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Cache-Control", "public, max-age=3600");

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
    }
}
