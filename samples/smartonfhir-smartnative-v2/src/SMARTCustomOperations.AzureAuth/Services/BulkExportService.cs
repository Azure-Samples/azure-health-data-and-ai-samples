// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public sealed partial class BulkExportService : IBulkExportService
    {
        private const string ExportFileRouteSegment = "_export";

        private static readonly Regex GroupExportExpression = GroupExportRegex();
        private static readonly Regex ExportStatusExpression = ExportStatusRegex();

        private readonly AzureAuthOperationsConfig _config;
        private readonly IFhirAccessTokenValidator _tokenValidator;
        private readonly IExportFileService _exportFileService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BulkExportService> _logger;

        public BulkExportService(
            AzureAuthOperationsConfig config,
            IFhirAccessTokenValidator tokenValidator,
            IExportFileService exportFileService,
            IHttpClientFactory httpClientFactory,
            ILogger<BulkExportService> logger)
        {
            _config = config;
            _tokenValidator = tokenValidator;
            _exportFileService = exportFileService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public BulkExportRequestType Classify(string method, string localPath)
        {
            var path = "/" + (localPath ?? string.Empty).TrimStart('/');

            if (method.Equals("GET", StringComparison.OrdinalIgnoreCase) && GroupExportExpression.IsMatch(path))
            {
                return BulkExportRequestType.GroupExportKickoff;
            }

            if (ExportStatusExpression.IsMatch(path))
            {
                if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    return BulkExportRequestType.ExportStatus;
                }

                if (method.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                {
                    return BulkExportRequestType.ExportDelete;
                }
            }

            return BulkExportRequestType.None;
        }

        public async Task<BulkExportResult> HandleAsync(BulkExportRequest request, CancellationToken cancellationToken = default)
        {
            FhirTokenValidationResult tokenResult;
            try
            {
                tokenResult = await _tokenValidator.ValidateAsync(request.AuthorizationHeader, cancellationToken);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Bulk export request unauthorized: {Message}", ex.Message);
                return OperationOutcome(HttpStatusCode.Unauthorized, "login", "Authentication required.");
            }

            var type = Classify(request.Method, request.LocalPath);
            return type switch
            {
                BulkExportRequestType.GroupExportKickoff => await HandleKickoffAsync(request, tokenResult.ClientId, cancellationToken),
                BulkExportRequestType.ExportStatus => await HandleStatusAsync(request, tokenResult.ClientId, cancellationToken),
                BulkExportRequestType.ExportDelete => await HandleDeleteAsync(request, tokenResult.ClientId, cancellationToken),
                _ => OperationOutcome(HttpStatusCode.BadRequest, "not-supported", "Unsupported bulk export request."),
            };
        }

        private async Task<BulkExportResult> HandleKickoffAsync(BulkExportRequest request, string clientId, CancellationToken cancellationToken)
        {
            var fhirBaseUrl = _config.FhirServerUrl!.TrimEnd('/');
            var container = ExportContainerNaming.ForClient(clientId);

            // Force the export output into this client's dedicated container, ignoring any
            // caller-supplied _container so clients cannot read one another's exports.
            var query = HttpUtility.ParseQueryString(request.Query ?? string.Empty);
            query.Remove("_container");
            query.Set("_container", container);

            var targetUrl = $"{fhirBaseUrl}{EnsureLeadingSlash(request.LocalPath)}?{query}";

            using var proxyRequest = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            AddHeader(proxyRequest, "Authorization", request.AuthorizationHeader);
            AddHeader(proxyRequest, "Accept", string.IsNullOrEmpty(request.AcceptHeader) ? "application/fhir+json" : request.AcceptHeader);
            AddHeader(proxyRequest, "Prefer", string.IsNullOrEmpty(request.PreferHeader) ? "respond-async" : request.PreferHeader);

            var client = _httpClientFactory.CreateClient("FhirProxy");
            using var fhirResponse = await client.SendAsync(proxyRequest, cancellationToken);

            if (fhirResponse.StatusCode != HttpStatusCode.Accepted)
            {
                // Pass through the FHIR error (e.g. 400/401/404) unchanged.
                return await PassthroughAsync(fhirResponse, cancellationToken);
            }

            var contentLocation = fhirResponse.Content.Headers.ContentLocation?.ToString()
                ?? (fhirResponse.Headers.TryGetValues("Content-Location", out var values) ? values.FirstOrDefault() : null);

            if (string.IsNullOrEmpty(contentLocation))
            {
                _logger.LogError("FHIR $export kickoff returned 202 without a Content-Location header.");
                return OperationOutcome(HttpStatusCode.InternalServerError, "exception", "Export kickoff did not return a status URL.");
            }

            var jobId = ExtractJobId(contentLocation);
            if (string.IsNullOrEmpty(jobId))
            {
                _logger.LogError("Unable to parse export job id from Content-Location {ContentLocation}.", contentLocation);
                return OperationOutcome(HttpStatusCode.InternalServerError, "exception", "Export kickoff returned an unrecognized status URL.");
            }

            // Stateless design: no job record is persisted. Client isolation is enforced entirely
            // by the per-client container name, which is checked on status and file requests.
            var result = new BulkExportResult
            {
                StatusCode = HttpStatusCode.Accepted,
            };
            result.Headers["Content-Location"] = $"{request.GatewayBaseUrl}/_operations/export/{jobId}";
            return result;
        }

        private async Task<BulkExportResult> HandleStatusAsync(BulkExportRequest request, string clientId, CancellationToken cancellationToken)
        {
            var jobId = ExtractJobId(request.LocalPath);
            if (string.IsNullOrEmpty(jobId))
            {
                return OperationOutcome(HttpStatusCode.BadRequest, "value", "Missing export job id.");
            }

            var fhirBaseUrl = _config.FhirServerUrl!.TrimEnd('/');
            var targetUrl = $"{fhirBaseUrl}/_operations/export/{jobId}";

            using var proxyRequest = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            AddHeader(proxyRequest, "Authorization", request.AuthorizationHeader);

            var client = _httpClientFactory.CreateClient("FhirProxy");
            using var fhirResponse = await client.SendAsync(proxyRequest, cancellationToken);

            // Still running: preserve async polling semantics.
            if (fhirResponse.StatusCode == HttpStatusCode.Accepted)
            {
                var running = new BulkExportResult { StatusCode = HttpStatusCode.Accepted };
                CopyHeaderIfPresent(fhirResponse, "Retry-After", running.Headers);
                CopyHeaderIfPresent(fhirResponse, "X-Progress", running.Headers);
                return running;
            }

            if (fhirResponse.StatusCode != HttpStatusCode.OK)
            {
                return await PassthroughAsync(fhirResponse, cancellationToken);
            }

            var body = await fhirResponse.Content.ReadAsStringAsync(cancellationToken);
            var expectedContainer = ExportContainerNaming.ForClient(clientId);

            // Ownership is enforced statelessly: a completed manifest lists file URLs under the
            // storage container the export wrote to. If that container is not this client's, the
            // caller is polling another client's job — reject it.
            if (!ManifestBelongsToClient(body, expectedContainer))
            {
                _logger.LogWarning("Client {ClientId} polled export job {JobId} it does not own.", clientId, jobId);
                return OperationOutcome(HttpStatusCode.NotFound, "not-found", "Export job not found.");
            }

            var rewritten = RewriteManifest(body, request.GatewayBaseUrl);

            return new BulkExportResult
            {
                StatusCode = HttpStatusCode.OK,
                Body = rewritten,
                ContentType = "application/json",
            };
        }

        private async Task<BulkExportResult> HandleDeleteAsync(BulkExportRequest request, string clientId, CancellationToken cancellationToken)
        {
            var jobId = ExtractJobId(request.LocalPath);
            if (string.IsNullOrEmpty(jobId))
            {
                return OperationOutcome(HttpStatusCode.BadRequest, "value", "Missing export job id.");
            }

            var fhirBaseUrl = _config.FhirServerUrl!.TrimEnd('/');
            var targetUrl = $"{fhirBaseUrl}/_operations/export/{jobId}";

            using var proxyRequest = new HttpRequestMessage(HttpMethod.Delete, targetUrl);
            AddHeader(proxyRequest, "Authorization", request.AuthorizationHeader);

            var client = _httpClientFactory.CreateClient("FhirProxy");
            using var fhirResponse = await client.SendAsync(proxyRequest, cancellationToken);
            return await PassthroughAsync(fhirResponse, cancellationToken);
        }

        // Returns true if the manifest contains no file URLs yet, or every file URL is under the
        // client's own container. Any URL under a different container means the caller does not own
        // this job.
        private static bool ManifestBelongsToClient(string body, string expectedContainer)
        {
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(body);
            }
            catch (JsonException)
            {
                return true;
            }

            if (root is not JsonObject manifest)
            {
                return true;
            }

            foreach (var arrayName in new[] { "output", "error", "deleted" })
            {
                if (manifest[arrayName] is JsonArray array)
                {
                    foreach (var item in array)
                    {
                        if (item is JsonObject file && file["url"] is JsonValue urlValue && urlValue.TryGetValue<string>(out var url)
                            && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        {
                            var container = uri.AbsolutePath.TrimStart('/').Split('/', 2)[0];
                            if (!string.Equals(container, expectedContainer, StringComparison.Ordinal))
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        // Rewrites the completed export manifest so every output/error/deleted file URL points at
        // the gateway file endpoint, and marks the files as requiring an access token.
        private string RewriteManifest(string body, string gatewayBaseUrl)
        {
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(body);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse export manifest for rewriting.");
                return body;
            }

            if (root is not JsonObject manifest)
            {
                return body;
            }

            manifest["requiresAccessToken"] = true;

            foreach (var arrayName in new[] { "output", "error", "deleted" })
            {
                if (manifest[arrayName] is JsonArray array)
                {
                    foreach (var item in array)
                    {
                        if (item is JsonObject file && file["url"] is JsonValue urlValue && urlValue.TryGetValue<string>(out var url))
                        {
                            file["url"] = BuildGatewayFileUrl(gatewayBaseUrl, url);
                        }
                    }
                }
            }

            return manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }

        // Maps an AHDS storage blob URL to a gateway file URL:
        //   https://acct.blob.core.windows.net/{container}/{path}  ->  {gateway}/_export/{container}/{path}
        private string BuildGatewayFileUrl(string gatewayBaseUrl, string storageUrl)
        {
            if (!Uri.TryCreate(storageUrl, UriKind.Absolute, out var uri))
            {
                return storageUrl;
            }

            var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
            var container = segments.Length > 0 ? segments[0] : string.Empty;
            var blobPath = segments.Length > 1 ? segments[1] : string.Empty;

            return $"{gatewayBaseUrl}/{ExportFileRouteSegment}/{container}/{blobPath}";
        }

        private static async Task<BulkExportResult> PassthroughAsync(HttpResponseMessage fhirResponse, CancellationToken cancellationToken)
        {
            var body = await fhirResponse.Content.ReadAsStringAsync(cancellationToken);
            var contentType = fhirResponse.Content.Headers.ContentType?.ToString() ?? "application/fhir+json";
            var result = new BulkExportResult
            {
                StatusCode = fhirResponse.StatusCode,
                Body = string.IsNullOrEmpty(body) ? null : body,
                ContentType = contentType,
            };
            CopyHeaderIfPresent(fhirResponse, "Content-Location", result.Headers);
            return result;
        }

        private static void CopyHeaderIfPresent(HttpResponseMessage response, string headerName, IDictionary<string, string> target)
        {
            if (response.Headers.TryGetValues(headerName, out var values) ||
                response.Content.Headers.TryGetValues(headerName, out values))
            {
                var value = values.FirstOrDefault();
                if (!string.IsNullOrEmpty(value))
                {
                    target[headerName] = value;
                }
            }
        }

        private static void AddHeader(HttpRequestMessage request, string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        private static string EnsureLeadingSlash(string path) => "/" + (path ?? string.Empty).TrimStart('/');

        private static string? ExtractJobId(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var match = ExportStatusExpression.Match("/" + value.TrimStart('/'));
            if (match.Success)
            {
                return match.Groups["id"].Value;
            }

            // Content-Location is an absolute URL; parse its path.
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                var m2 = ExportStatusExpression.Match(uri.AbsolutePath);
                if (m2.Success)
                {
                    return m2.Groups["id"].Value;
                }
            }

            return null;
        }

        // Derives a stable, valid blob container name (3-63 chars, lowercase alphanumeric) unique
        // per client from a hash of the validated client id.
        public bool IsExportFileRequest(string localPath)
        {
            var path = "/" + (localPath ?? string.Empty).TrimStart('/');
            return path.StartsWith($"/{ExportFileRouteSegment}/", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ExportFileResult> GetExportFileAsync(string localPath, string? authorizationHeader, CancellationToken cancellationToken = default)
        {
            FhirTokenValidationResult tokenResult;
            try
            {
                tokenResult = await _tokenValidator.ValidateAsync(authorizationHeader, cancellationToken);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Export file request unauthorized: {Message}", ex.Message);
                return new ExportFileResult
                {
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorBody = OperationOutcomeBody("login", "Authentication required."),
                };
            }

            var (container, blobPath) = ParseExportFilePath(localPath);
            if (string.IsNullOrEmpty(container) || string.IsNullOrEmpty(blobPath))
            {
                return new ExportFileResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorBody = OperationOutcomeBody("value", "Malformed export file path."),
                };
            }

            // The caller may only read files in their own container.
            var expectedContainer = ExportContainerNaming.ForClient(tokenResult.ClientId);
            if (!string.Equals(container, expectedContainer, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Client {ClientId} attempted to access container {Container} it does not own.",
                    tokenResult.ClientId,
                    container);
                return new ExportFileResult
                {
                    StatusCode = HttpStatusCode.Forbidden,
                    ErrorBody = OperationOutcomeBody("forbidden", "You are not authorized to access this export file."),
                };
            }

            Stream? stream;
            try
            {
                stream = await _exportFileService.OpenReadAsync(container, blobPath, cancellationToken);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Gateway failed to read export file {Container}/{Blob}.", container, blobPath);
                return new ExportFileResult
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    ErrorBody = OperationOutcomeBody("exception", "Unable to read the export file."),
                };
            }

            if (stream is null)
            {
                return new ExportFileResult
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ErrorBody = OperationOutcomeBody("not-found", "Export file not found."),
                };
            }

            return new ExportFileResult
            {
                StatusCode = HttpStatusCode.OK,
                Content = stream,
            };
        }

        private (string container, string blobPath) ParseExportFilePath(string localPath)
        {
            var path = (localPath ?? string.Empty).TrimStart('/');
            var segments = path.Split('/');

            // Expected: _export/{container}/{blobPath...}
            var index = Array.FindIndex(segments, s => s.Equals(ExportFileRouteSegment, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || segments.Length < index + 3)
            {
                return (string.Empty, string.Empty);
            }

            var container = segments[index + 1];
            var blobPath = string.Join('/', segments.Skip(index + 2));
            return (container, blobPath);
        }

        private static string OperationOutcomeBody(string code, string message)
        {
            return new JsonObject
            {
                ["resourceType"] = "OperationOutcome",
                ["issue"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["severity"] = "error",
                        ["code"] = code,
                        ["diagnostics"] = message,
                    },
                },
            }.ToJsonString();
        }

        private static BulkExportResult OperationOutcome(HttpStatusCode statusCode, string code, string message)
        {
            var outcome = new JsonObject
            {
                ["resourceType"] = "OperationOutcome",
                ["issue"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["severity"] = "error",
                        ["code"] = code,
                        ["diagnostics"] = message,
                    },
                },
            };

            return new BulkExportResult
            {
                StatusCode = statusCode,
                Body = outcome.ToJsonString(),
                ContentType = "application/fhir+json",
            };
        }

        [GeneratedRegex(@"^/Group/(?<id>[A-Za-z0-9\-\.]+)/\$export$", RegexOptions.IgnoreCase)]
        private static partial Regex GroupExportRegex();

        [GeneratedRegex(@"^/_operations/export/(?<id>[A-Za-z0-9\-\.]+)$", RegexOptions.IgnoreCase)]
        private static partial Regex ExportStatusRegex();
    }
}
