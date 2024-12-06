// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ConsentSample.FhirOperation;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;

namespace ConsentSample.Processors
{
    public class FhirProcessor : IFhirProcessor
    {
        private readonly ILogger<FhirProcessor>? _logger;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient? _telemetryClient;

        public FhirProcessor(IFhirClient fhirClient, TelemetryClient? telemetryClient, ILogger<FhirProcessor>? logger)
        {
            _logger = logger;
            _fhirClient = fhirClient;
            _telemetryClient = telemetryClient;
        }

        public virtual async Task<HttpResponseMessage> CallProcess(HttpMethod method, string requestContent, Uri baseUri, string queryString, string endpoint)
        {
            try
            {
                var request = new HttpRequestMessage
                {
                    Method = method,
                    RequestUri = new Uri(baseUri, queryString),
                    Headers =
                    {
                        { HttpRequestHeader.Accept.ToString(), "application/json" },
                    },
                    Content = new StringContent(requestContent, Encoding.UTF8, "application/fhir+json"),
                };

                HttpResponseMessage fhirResponse = await _fhirClient.Send(request, baseUri, endpoint);
                return fhirResponse;
            }
            catch
            {
                _logger?.LogError($"Error occurred at FhirProcessor:CallProcess().");
                throw;
            }
        }
    }
}
