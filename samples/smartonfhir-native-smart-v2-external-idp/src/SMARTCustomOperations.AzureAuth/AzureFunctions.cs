// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;

namespace SMARTCustomOperations.AzureAuth
{
    public class AzureFunctions
    {
        private readonly ILogger _logger;
        private readonly IPipeline<HttpRequestData, HttpResponseData> _pipeline;

        public AzureFunctions(ILogger<AzureFunctions> logger, IPipeline<HttpRequestData, HttpResponseData> pipeline)
        {
            _logger = logger;
            _pipeline = pipeline;
        }

        [Function("Token")]
        public async Task<HttpResponseData> RunTokenFunction([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/token")] HttpRequestData req)
        {
            _logger.LogInformation("Token function pipeline started.");
            return await _pipeline.ExecuteAsync(req);
        }

        [Function("ContextCache")]
        public async Task<HttpResponseData> RunContextCache([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/context-cache")] HttpRequestData req)
        {
            _logger.LogInformation("ContextCache function pipeline started.");
            return await _pipeline.ExecuteAsync(req);
        }
    }
}
