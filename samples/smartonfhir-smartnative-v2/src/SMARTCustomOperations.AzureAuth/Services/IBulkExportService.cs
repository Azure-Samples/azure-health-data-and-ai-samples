// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public enum BulkExportRequestType
    {
        None,
        GroupExportKickoff,
        ExportStatus,
        ExportDelete,
    }

    /// <summary>
    /// Transport-agnostic result of a bulk export operation, mapped to an HTTP response by the caller.
    /// </summary>
    public sealed class BulkExportResult
    {
        public HttpStatusCode StatusCode { get; init; }

        public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public string? Body { get; init; }

        public string ContentType { get; init; } = "application/fhir+json";
    }

    /// <summary>
    /// Inputs required to service a bulk export request, decoupled from Azure Functions types.
    /// </summary>
    public sealed class BulkExportRequest
    {
        public required string Method { get; init; }

        public required string LocalPath { get; init; }

        public required string Query { get; init; }

        public string? AuthorizationHeader { get; init; }

        public string? AcceptHeader { get; init; }

        public string? PreferHeader { get; init; }

        /// <summary>Gateway base URL (scheme://authority) used when rewriting public URLs.</summary>
        public required string GatewayBaseUrl { get; init; }
    }

    /// <summary>
    /// Result of an authenticated export file request. When <see cref="Content"/> is non-null the
    /// caller streams it to the response; otherwise <see cref="StatusCode"/> and
    /// <see cref="ErrorBody"/> describe the failure.
    /// </summary>
    public sealed class ExportFileResult
    {
        public HttpStatusCode StatusCode { get; init; }

        public Stream? Content { get; init; }

        public string? ErrorBody { get; init; }
    }

    /// <summary>
    /// Handles SMART Backend Services bulk data export: group export kickoff, status polling,
    /// and cancellation. Enforces per-client isolation and rewrites FHIR/storage URLs so clients
    /// only ever see gateway URLs and can only reach their own export output.
    /// </summary>
    public interface IBulkExportService
    {
        BulkExportRequestType Classify(string method, string localPath);

        Task<BulkExportResult> HandleAsync(BulkExportRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns true when the path targets the gateway export-file endpoint (/_export/...).
        /// </summary>
        bool IsExportFileRequest(string localPath);

        /// <summary>
        /// Validates the access token, verifies the caller owns the container, and opens a read
        /// stream over the requested export file.
        /// </summary>
        Task<ExportFileResult> GetExportFileAsync(string localPath, string? authorizationHeader, CancellationToken cancellationToken = default);
    }
}
