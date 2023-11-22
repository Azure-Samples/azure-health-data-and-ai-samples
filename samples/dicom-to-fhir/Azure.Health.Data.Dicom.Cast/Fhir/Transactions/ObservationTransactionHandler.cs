// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Immutable;
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

internal class ObservationTransactionHandler(FhirClient client, ILogger<ObservationTransactionHandler> logger)
{
    private readonly FhirClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly ILogger<ObservationTransactionHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask<Patient> AddOrUpdateObservationAsync(
        TransactionBuilder builder,
        DicomDataset dataset,
        Endpoint endpoint,
        Patient patient,
        ImagingStudy study,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(study);

        List<Observation> newObservations = CreateObservations(dataset, patient.GetReference(), study.GetReference());
        if (newObservations.Count > 0)
        {
            foreach ()
        }
    }

    private List<Observation> CreateObservations(DicomDataset dataset, ResourceReference patient, ResourceReference imagingStudy)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(imagingStudy);
        ArgumentNullException.ThrowIfNull(patient);

        List<Observation> observations = [];

        if (dataset.TryGetSequence(DicomTag.ConceptNameCodeSequence, out DicomSequence codes) && codes?.Items.Count > 0)
        {
            DicomCodeItem code = new(codes);

            if (dataset.TryCreateIrradiationEvent(code, patient, out Observation? observation))
                observations.Add(observation);

            if (dataset.TryCreateDoseSummary(code, patient, imagingStudy, out observation))
                observations.Add(observation);
        }

        // Create observations for each of the content items
        if (dataset.TryGetSequence(DicomTag.ContentSequence, out DicomSequence content) && content?.Items.Count > 0)
            observations.AddRange(content.Items.SelectMany(c => CreateObservations(c, patient, imagingStudy)));

        return observations;
    }

    private async IAsyncEnumerable<Observation> GetObservationsAsync(Identifier imagingStudy, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        SearchParams parameters = new SearchParams().Add("identifier", $"{imagingStudy.System}|{imagingStudy.Value}");
        Bundle? bundle = await _client.SearchAsync<Observation>(parameters, cancellationToken);
        if (bundle is not null)
        {
            await foreach (Observation o in bundle.GetEntriesAsync(_client).Select(x => x.Resource).Cast<Observation>())
                yield return o;
        }
    }
}
