// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Security;
using Microsoft.AzureHealth.DataServices.Clients.Headers;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Extensions;
using SMARTCustomOperations.Export.Configuration;
using SMARTCustomOperations.Export.Services;

namespace SMARTCustomOperations.Export.Filters
{
    public class GetExportFileInputFilter : IInputFilter
    {
        private readonly ExportCustomOperationsConfig _configuration;
        private readonly ILogger _logger;
        private readonly IExportFileService _exportFileService;
        private readonly string _id;

        public GetExportFileInputFilter(ExportCustomOperationsConfig configuration, IExportFileService exportFileService, ILogger<GetExportFileInputFilter> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _exportFileService = exportFileService;
            _id = Guid.NewGuid().ToString();
        }

        public event EventHandler<FilterErrorEventArgs>? OnFilterError;

        public string Name => nameof(GetExportFileInputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;

        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            _logger?.LogInformation("Entered {Name}", Name);

            if (!IsGetExportFileOperation(context))
            {
                return context;
            }

            try
            {
                // Get blob info from the Uri
                (string, string) blobPathInfo = GetContainerAndBlobFromRequestUrl(context);

                // Ensure user is accessing their own export
                if (!string.Equals(blobPathInfo.Item1, context.Properties["oid"], StringComparison.InvariantCulture))
                {
                    var ex = new SecurityException($"User attempted export access with token with wrong oid claim. {Id}. OID: {context.Properties["oid"]}. Container: {blobPathInfo.Item1}.");

                    FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.Unauthorized);
                    OnFilterError?.Invoke(this, error);
                    return context.SetContextErrorBody(error, _configuration.Debug);
                }

                context.Content = await _exportFileService.GetContent(blobPathInfo.Item1, blobPathInfo.Item2);

                // Inform the pipeline to skip the binding
                context.StatusCode = HttpStatusCode.OK;
                context.IsFatal = true;

                context.Headers.Add(new HeaderNameValuePair("Content-Type", "application/fhir+ndjson", CustomHeaderType.ResponseStatic));
            }
            catch (Exception ex)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: new Exception($"Could not process export check result.", ex), code: HttpStatusCode.InternalServerError, responseBody: context.ContentString);
                OnFilterError?.Invoke(this, error);
                context.IsFatal = true;
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            return context;
        }

        private static bool IsGetExportFileOperation(OperationContext context)
        {
            return context.Properties["PipelineType"] == ExportOperationType.GetExportFile.ToString();
        }

        private (string, string) GetContainerAndBlobFromRequestUrl(OperationContext context)
        {
            string localPath = context.Request!.RequestUri!.LocalPath;
            IEnumerable<string> segments = localPath.TrimStart('/').Split('/').ToList();


            // Skip up to and past the _export element
            segments = segments.SkipWhile(x => x != "_export").Skip(1);

            // Get the container and skip the blob
            string containerName = segments.First();
            string blobName = string.Join('/', segments.Skip(1));

            return (containerName, blobName);
        }
    }
}
