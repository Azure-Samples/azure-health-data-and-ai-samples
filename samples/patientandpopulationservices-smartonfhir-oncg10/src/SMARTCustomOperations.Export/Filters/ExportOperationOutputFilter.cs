﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Extensions;
using SMARTCustomOperations.Export.Configuration;

namespace SMARTCustomOperations.Export.Filters
{
    public class ExportOperationOutputFilter : IOutputFilter
    {
        private readonly ILogger _logger;
        private readonly ExportCustomOperationsConfig _configuration;
        private readonly string _id;

        public ExportOperationOutputFilter(ILogger<ExportOperationOutputFilter> logger, ExportCustomOperationsConfig configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _id = Guid.NewGuid().ToString();
        }

#pragma warning disable CS0067 // Needed to implement interface.
        public event EventHandler<FilterErrorEventArgs>? OnFilterError;
#pragma warning restore CS0067 // Needed to implement interface.

        public string Name => nameof(ExportOperationOutputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;

        public Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            _logger?.LogInformation("Entered {Name}", Name);

            // Only execute filter for successful $export operations
            if (context.Properties["PipelineType"] != ExportOperationType.GroupExport.ToString() || context.StatusCode != HttpStatusCode.Accepted)
            {
                return Task.FromResult(context);
            }

            // Replace the content location URL with the public endpoint
            var contentLocationHeader = context.Headers.FirstOrDefault(x => x.Name.Equals("Content-Location", StringComparison.OrdinalIgnoreCase));

            if (contentLocationHeader is not null)
            {
                // Toolkit does not support setting Content output headers
                contentLocationHeader.Name = "Custom-Content-Locaton";
                contentLocationHeader.Value =
                    contentLocationHeader.Value.Replace(
                        _configuration.FhirServerUrl!,
                        $"https://{_configuration.ApiManagementHostName}/{_configuration.ApiManagementFhirPrefex}",
                        StringComparison.OrdinalIgnoreCase);

                return Task.FromResult(context);
            }

#pragma warning disable CA2201 // #TODO - add more specific exception.
            FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: new Exception("Content Location not found."), code: HttpStatusCode.InternalServerError);
            OnFilterError?.Invoke(this, error);
            return Task.FromResult(context.SetContextErrorBody(error, _configuration.Debug));
        }
    }
}
