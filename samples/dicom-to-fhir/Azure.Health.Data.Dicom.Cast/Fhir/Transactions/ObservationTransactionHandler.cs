// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using FellowOakDicom.StructuredReport;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal sealed class ObservationTransactionHandler(FhirClient client, ILogger<ObservationTransactionHandler> logger)
{
    private readonly FhirClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly ILogger<ObservationTransactionHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask<ResourceTransactionBuilder<IReadOnlyList<Observation>>> AddOrUpdateObservationsAsync(
        TransactionBuilder builder,
        DicomDataset dataset,
        Endpoint endpoint,
        Patient patient,
        ImagingSelection selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(selection);

        Identifier identifier = dataset.GetSopInstanceIdentifier();
        List<Observation> existingObservations = await GetObservationsAsync(identifier, cancellationToken).ToListAsync(cancellationToken);
        if (existingObservations.Count > 0)
        {
            // Delete the old existingObservations for this imaging selection
            foreach (Observation previous in existingObservations)
                builder = builder.Delete(nameof(Observation), previous.Id);
        }

        // TODO: Is there a way to "deduplicate" the existingObservations?
        List<Observation> newObservations = CreateObservations(dataset, endpoint, patient.GetReference(), selection.GetReference());
        foreach (Observation observation in newObservations)
            builder = builder.Create(observation);

        _logger.LogInformation(
            "Replacing {ExistingCount} Observation resources with {NewCount} new Observation resources.",
            existingObservations.Count,
            newObservations.Count);

        return builder.ForResource<IReadOnlyList<Observation>>(newObservations);
    }

    public TransactionBuilder DeleteObservations(TransactionBuilder builder, InstanceIdentifiers identifiers)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _logger.LogInformation("Deleting all Observation resources based on the DICOM SOP instance.");
        Identifier identifier = DicomIdentifier.FromUid(identifiers.SopInstanceUid);
        return builder.Delete(nameof(ResourceType.ImagingSelection), new SearchParams().Add(identifier));
    }

    private List<Observation> CreateObservations(
        DicomDataset dataset,
        Endpoint endpoint,
        ResourceReference patient,
        ResourceReference imagingSelection)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(imagingSelection);

        List<Observation> observations = [];

        if (dataset.TryGetSequence(DicomTag.ConceptNameCodeSequence, out DicomSequence codes) && codes?.Items.Count > 0)
        {
            DicomCodeItem code = new(codes);

            if (dataset.TryCreateIrradiationEvent(code, endpoint, patient, imagingSelection, out Observation? observation))
                observations.Add(observation);

            if (dataset.TryCreateDoseSummary(code, endpoint, patient, imagingSelection, out observation))
                observations.Add(observation);
        }

        // Create existingObservations for each of the content items
        if (dataset.TryGetSequence(DicomTag.ContentSequence, out DicomSequence content) && content?.Items.Count > 0)
            observations.AddRange(content.Items.SelectMany(c => CreateObservations(c, endpoint, patient, imagingSelection)));

        return observations;
    }

    private async IAsyncEnumerable<Observation> GetObservationsAsync(Identifier identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Bundle? bundle = await _client.SearchAsync<Observation>(new SearchParams().Add(identifier), cancellationToken);
        if (bundle is not null)
        {
            await foreach (Observation o in bundle.GetPagesAsync(_client).SelectMany(x => x.Entry).Select(x => x.Resource).Cast<Observation>())
                yield return o;
        }
    }
}
