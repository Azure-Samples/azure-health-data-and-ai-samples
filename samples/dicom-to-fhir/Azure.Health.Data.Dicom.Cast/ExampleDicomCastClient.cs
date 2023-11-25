// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Health.Data.Dicom.Cast.DicomWeb;
using Azure.Health.Data.Dicom.Cast.Fhir;
using Azure.Health.Data.Dicom.Cast.Fhir.Transactions;
using Azure.Messaging.EventGrid.SystemEvents;
using FellowOakDicom;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Utility;
using Microsoft.Extensions.Logging;

namespace Azure.Health.Data.Dicom.Cast;

internal sealed class ExampleDicomCastClient(
    DicomWebClient dicomWebClient,
    FhirClient fhirClient,
    DicomTransactionBuilder transactionBuilder,
    ILogger<ExampleDicomCastClient> logger) : IDicomCastClient
{
    private readonly DicomWebClient _dicomWebClient = dicomWebClient ?? throw new ArgumentNullException(nameof(dicomWebClient));
    private readonly FhirClient _fhirClient = fhirClient ?? throw new ArgumentNullException(nameof(fhirClient));
    private readonly DicomTransactionBuilder _transactionBuilder = transactionBuilder ?? throw new ArgumentNullException(nameof(transactionBuilder));
    private readonly ILogger<ExampleDicomCastClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public ValueTask CreateObservationsAsync(HealthcareDicomImageCreatedEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        return UpsertObservationsAsync(
            new InstanceIdentifiers(eventData.ImageStudyInstanceUid, eventData.ImageSeriesInstanceUid, eventData.ImageSopInstanceUid),
            cancellationToken);
    }

    public ValueTask UpdateObservationsAsync(HealthcareDicomImageUpdatedEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        return UpsertObservationsAsync(
            new InstanceIdentifiers(eventData.ImageStudyInstanceUid, eventData.ImageSeriesInstanceUid, eventData.ImageSopInstanceUid),
            cancellationToken);
    }

    public async ValueTask DeleteObservationsAsync(HealthcareDicomImageDeletedEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Bundle transaction = await _transactionBuilder.CreateDeleteBundleAsync(
            new InstanceIdentifiers(eventData.ImageStudyInstanceUid, eventData.ImageSeriesInstanceUid, eventData.ImageSopInstanceUid),
            cancellationToken);

        Bundle response = await _fhirClient.TransactionAsync(transaction, cancellationToken) ?? throw new FhirOperationException("Received unexpected response from FHIR server", 0);
        _ = await response
            .GetPagesAsync(_fhirClient)
            .Select(x => x.EnsureSuccessStatusCodes())
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Successfully deleted or updated all related FHIR resources.");
    }

    private async ValueTask UpsertObservationsAsync(InstanceIdentifiers instanceIdentifiers, CancellationToken cancellationToken)
    {
        DicomDataset dataset;
        try
        {
            dataset = await _dicomWebClient.RetrieveInstanceMetadataAsync(instanceIdentifiers, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Cannot find DICOM SOP instance using WADO-RS. Event may have come out-of-order, or a delete event may be arrive shortly.");
            return;
        }

        Bundle transaction = await _transactionBuilder.CreateUpsertBundleAsync(dataset, cancellationToken);
        Bundle response = await _fhirClient.TransactionAsync(transaction, cancellationToken) ?? throw new FhirOperationException("Received unexpected response from FHIR server", 0);

        // Validate the operations where another process may have created the resource instead of us
        bool interrupted = await response
            .GetPagesAsync(_fhirClient)
            .SelectMany(x => x.EnsureSuccessStatusCodes().Entry)
            .Where(x => x.Resource is Patient or ImagingStudy or ImagingSelection)
            .Where(x => x.Response.Annotation<HttpStatusCode>() == HttpStatusCode.Created)
            .AnyAsync(cancellationToken);

        if (interrupted)
            throw new InvalidOperationException("Another process created one or more resources related to the DICOM SOP instance.");

        _logger.LogInformation("Successfully upserted all related FHIR resources.");
    }
}
