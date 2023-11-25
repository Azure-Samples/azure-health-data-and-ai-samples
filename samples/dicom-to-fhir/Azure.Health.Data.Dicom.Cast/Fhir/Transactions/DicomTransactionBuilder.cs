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

internal class DicomTransactionBuilder(
    EndpointTransactionHandler endpointHandler,
    PatientTransactionHandler patientHandler,
    ImagingStudyTransactionHandler imagingStudyHandler,
    ImagingSelectionTransactionHandler imagingSelectionHandler,
    ObservationTransactionHandler observationHandler,
    IOptionsSnapshot<FhirClientOptions> options)
{
    private readonly EndpointTransactionHandler _endpointHandler = endpointHandler ?? throw new ArgumentNullException(nameof(endpointHandler));
    private readonly PatientTransactionHandler _patientHandler = patientHandler ?? throw new ArgumentNullException(nameof(patientHandler));
    private readonly ImagingStudyTransactionHandler _imagingStudyHandler = imagingStudyHandler ?? throw new ArgumentNullException(nameof(imagingStudyHandler));
    private readonly ImagingSelectionTransactionHandler _imagingSelectionHandler = imagingSelectionHandler ?? throw new ArgumentNullException(nameof(imagingSelectionHandler));
    private readonly ObservationTransactionHandler _observationHandler = observationHandler ?? throw new ArgumentNullException(nameof(observationHandler));
    private readonly FhirClientOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async ValueTask<Bundle> CreateUpsertBundleAsync(DicomDataset dataset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        TransactionBuilder builder = new(_options.ServiceUri, Bundle.BundleType.Transaction);

        // Ensure the endpoint resource exists for the WADO-RS endpoint
        ResourceTransactionBuilder<Endpoint> endpointBuilder = await _endpointHandler.GetOrAddEndpointAsync(builder, cancellationToken);

        // Ensure the patient exists for the SOP instance
        ResourceTransactionBuilder<Patient> patientBuilder = await _patientHandler.AddOrUpdatePatientAsync(
            endpointBuilder,
            dataset,
            endpointBuilder.Resource,
            cancellationToken);

        // Create or update the imaging study that contains the SOP instance
        ResourceTransactionBuilder<ImagingStudy> imagingStudyBuilder = await _imagingStudyHandler.AddOrUpdateImagingStudyAsync(
            patientBuilder,
            dataset,
            endpointBuilder.Resource,
            patientBuilder.Resource,
            cancellationToken);

        // Create the imaging selection that refers to the SOP instance
        ResourceTransactionBuilder<ImagingSelection> imagingSelectionBuilder = await _imagingSelectionHandler.AddOrUpdateImagingSelectionAsync(
            imagingStudyBuilder,
            dataset,
            endpointBuilder.Resource,
            patientBuilder.Resource,
            imagingStudyBuilder.Resource,
            cancellationToken);

        // Create observations derived from the SOP instance
        builder = await _observationHandler.AddOrUpdateObservationsAsync(
            imagingSelectionBuilder,
            dataset,
            endpointBuilder.Resource,
            patientBuilder.Resource,
            imagingSelectionBuilder.Resource,
            cancellationToken);

        return builder.ToBundle();
    }

    public async ValueTask<Bundle> CreateDeleteBundleAsync(InstanceIdentifiers identifiers, CancellationToken cancellationToken = default)
    {
        TransactionBuilder builder = new(_options.ServiceUri, Bundle.BundleType.Transaction);

        // Update or delete the imaging study that contains the SOP instance depending on whether it is the last SOP instance in the study
        builder = await _imagingStudyHandler.UpdateOrDeleteImagingStudyAsync(builder, identifiers, cancellationToken);

        // Delete the imaging selection that refers to the SOP instance
        builder = _imagingSelectionHandler.DeleteImagingSelection(builder, identifiers);

        // Delete the observations derived from the SOP instance
        builder = _observationHandler.DeleteObservations(builder, identifiers);

        return builder.ToBundle();
    }
}
