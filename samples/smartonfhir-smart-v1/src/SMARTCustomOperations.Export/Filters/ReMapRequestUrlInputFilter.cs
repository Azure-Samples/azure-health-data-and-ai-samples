// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using Microsoft.AspNetCore.Http.Internal;
using System.Security.Cryptography;
using Microsoft.AzureHealth.DataServices.Clients.Headers;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Extensions;
using SMARTCustomOperations.Export.Configuration;
using System.Collections.Specialized;

namespace SMARTCustomOperations.Export.Filters
{
    public class ReMapRequestUrlInputFilter : IInputFilter
    {
        private readonly ILogger _logger;
        private readonly ExportCustomOperationsConfig _configuration;
        private readonly string _id;

        public ReMapRequestUrlInputFilter(ILogger<ReMapRequestUrlInputFilter> logger, ExportCustomOperationsConfig configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _id = Guid.NewGuid().ToString();
        }

        public event EventHandler<FilterErrorEventArgs>? OnFilterError;

        public string Name => nameof(ReMapRequestUrlInputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;

        public Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            _logger?.LogInformation("Entered {Name}", Name);

            try
            {
                if (context.Request?.RequestUri?.LocalPath.Contains("api/") ?? false)
                {
                    var baseUrl = context.Request.RequestUri!.GetLeftPart(UriPartial.Authority);
                    var path = context.Request?.RequestUri?.LocalPath.Replace("api/", string.Empty);
                    NameValueCollection queryCollection = context.Request!.RequestUri!.ParseQueryString();

                    if (context.Properties["PipelineType"] == ExportOperationType.GroupExport.ToString() && context.Properties.ContainsKey("oid"))
                    {
                        queryCollection.Remove("_container");
                        queryCollection.Add("_container", context.Properties["oid"]);
                    }
                    string? query = queryCollection.AllKeys.Count() > 0 ? queryCollection.ToString() : null;
                    context.UpdateRequestUri(context.Request?.Method, baseUrl, path, query);
                }

                return Task.FromResult(context);
            }
            catch (Exception ex)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.InternalServerError);
                OnFilterError?.Invoke(this, error);
                return Task.FromResult(context.SetContextErrorBody(error, _configuration.Debug));
            }
            
        }
    }
}
