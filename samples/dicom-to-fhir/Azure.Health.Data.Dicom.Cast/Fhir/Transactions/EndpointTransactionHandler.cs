// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Health.Data.Dicom.Cast.DicomWeb;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal sealed class EndpointTransactionHandler
{
    private readonly FhirClient _client;
    private readonly Uri _dicomServiceUri;
    private readonly string _endpointName;
    private readonly ILogger<EndpointTransactionHandler> _logger;

    public EndpointTransactionHandler(
        FhirClient client,
        IOptionsSnapshot<DicomWebClientOptions> options,
        ILogger<EndpointTransactionHandler> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DicomWebClientOptions dicomOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _dicomServiceUri = dicomOptions.ServiceUri;
        _endpointName = $"Azure Dicom Service {dicomOptions.Workspace}/{dicomOptions.Service} WADO-RS Endpoint";
    }

    public async ValueTask<ResourceTransactionBuilder<Endpoint>> GetOrAddEndpointAsync(TransactionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Whether we're updating and deleting DICOM SOP instances, ensure the endpoint is present in FHIR
        Endpoint? endpoint = await GetEndpointOrDefaultAsync(cancellationToken);
        if (endpoint is null)
        {
            endpoint = new()
            {
                Address = _dicomServiceUri.AbsoluteUri,
                ConnectionType =
                [
                    // "dicom-wado-rs" is a well-defined Endpoint Connection Type code
                    new("http://terminology.hl7.org/CodeSystem/endpoint-connection-type", "dicom-wado-rs")
                ],
                Id = $"urn:uuid:{Guid.NewGuid()}",
                Name = _endpointName,
                Payload =
                [
                    new()
                    {
                        // "DICOM WADO-RS" is a well-defined Endpoint Connection Type display name
                        MimeType = new List<string> { "application/dicom" },
                        Type = [new(string.Empty, string.Empty, "DICOM WADO-RS")],
                    }
                ],
                Status = Endpoint.EndpointStatus.Active,
            };

            _logger.LogInformation("Creating new Endpoint resource for Azure DICOM Service with address {ServiceUrl}.", _dicomServiceUri.AbsoluteUri);
            builder = builder.Create(endpoint, GetSearchParamsQuery());
        }
        else if (!string.Equals(endpoint.Address, _dicomServiceUri.AbsoluteUri, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Exceptions.EndpointMismatch,
                    _dicomServiceUri,
                    _endpointName,
                    endpoint.Address));
        }

        return builder.ForResource(endpoint);
    }

    private async ValueTask<Endpoint?> GetEndpointOrDefaultAsync(CancellationToken cancellationToken)
    {
        Bundle? bundle = await _client.SearchAsync<Endpoint>(GetSearchParamsQuery(), cancellationToken);
        if (bundle is null)
            return null;

        return await bundle
            .GetPagesAsync(_client)
            .SelectMany(x => x.Entry)
            .Select(x => x.Resource)
            .Cast<Endpoint>()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private SearchParams GetSearchParamsQuery()
    {
        return new SearchParams()
            .Add("name", _endpointName)
            .Add("connection-type", "http://terminology.hl7.org/CodeSystem/endpoint-connection-type|dicom-wado-rs");
    }
}