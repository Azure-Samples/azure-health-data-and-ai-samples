// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
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
    private readonly FhirClientOptions _options;

    public DicomTransactionBuilder(
        EndpointTransactionHandler endpointHandler,
        PatientTransactionHandler patientHandler,
        ImagingStudyTransactionHandler imagingStudyHandler,
        IOptionsSnapshot<FhirClientOptions> options)
    {
        _endpointHandler = endpointHandler ?? throw new ArgumentNullException(nameof(endpointHandler));
        _patientHandler = patientHandler ?? throw new ArgumentNullException(nameof(patientHandler));
        _imagingStudyHandler = imagingStudyHandler ?? throw new ArgumentNullException(nameof(imagingStudyHandler));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async ValueTask<Bundle> CreateUpsertBundleAsync(DicomDataset dataset, CancellationToken cancellationToken = default)
    {
        if (dataset is null)
            throw new ArgumentNullException(nameof(dataset));

        TransactionBuilder builder = new(_options.ServiceUri, Bundle.BundleType.Transaction);
        Endpoint endpoint = await _endpointHandler.GetOrAddEndpointAsync(builder, cancellationToken);
        Patient patient = await _patientHandler.AddOrUpdatePatientAsync(builder, dataset, cancellationToken);
        ImagingStudy imagingStudy = await _imagingStudyHandler.AddOrUpdateImagingStudyAsync(builder, dataset, endpoint, patient, cancellationToken);

        return builder.ToBundle();
    }

    public async ValueTask<Bundle> CreateDeleteBundleAsync(DicomDataset dataset, CancellationToken cancellationToken = default)
    {
    }
