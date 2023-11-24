// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Options;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal class DicomTransactionBuilder
{
    private readonly EndpointTransactionHandler _endpointHandler;
    private readonly PatientTransactionHandler _patientHandler;
    private readonly ImagingStudyTransactionHandler _imagingStudyHandler;
    private readonly ImagingSelectionTransactionHandler _imagingSelectionHandler;
    private readonly ObservationTransactionHandler _observationHandler;
    private readonly FhirClientOptions _options;

    public DicomTransactionBuilder(
        EndpointTransactionHandler endpointHandler,
        PatientTransactionHandler patientHandler,
        ImagingStudyTransactionHandler imagingStudyHandler,
        ImagingSelectionTransactionHandler imagingSelectionHandler,
        ObservationTransactionHandler observationHandler,
        IOptionsSnapshot<FhirClientOptions> options)
    {
        _endpointHandler = endpointHandler ?? throw new ArgumentNullException(nameof(endpointHandler));
        _patientHandler = patientHandler ?? throw new ArgumentNullException(nameof(patientHandler));
        _imagingStudyHandler = imagingStudyHandler ?? throw new ArgumentNullException(nameof(imagingStudyHandler));
        _imagingSelectionHandler = imagingSelectionHandler ?? throw new ArgumentNullException(nameof(imagingSelectionHandler));
        _observationHandler = observationHandler ?? throw new ArgumentNullException(nameof(observationHandler));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async ValueTask<Bundle> CreateUpsertBundleAsync(DicomDataset dataset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        TransactionBuilder builder = new(_options.ServiceUri, Bundle.BundleType.Transaction);
        Endpoint endpoint = await _endpointHandler.GetOrAddEndpointAsync(builder, cancellationToken);
        Patient patient = await _patientHandler.AddOrUpdatePatientAsync(builder, dataset, endpoint, cancellationToken);
        ImagingStudy imagingStudy = await _imagingStudyHandler.AddOrUpdateImagingStudyAsync(builder, dataset, endpoint, patient, cancellationToken);
        ImagingSelection imagingSelection = await _imagingSelectionHandler.AddOrUpdateImagingSelectionAsync(builder, dataset, endpoint, patient, imagingStudy, cancellationToken);
        IReadOnlyList<Observation> observations = await _observationHandler.AddOrUpdateObservationsAsync(builder, dataset, endpoint, patient, imagingSelection, cancellationToken);

        return builder.ToBundle();
    }

    public async ValueTask<Bundle> CreateDeleteBundleAsync(DicomDataset dataset, CancellationToken cancellationToken = default)
    {
    }
