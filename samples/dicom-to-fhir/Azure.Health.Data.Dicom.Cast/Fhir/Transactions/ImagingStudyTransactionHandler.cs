// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal class ImagingStudyTransactionHandler
{
    private readonly FhirClient _client;
    private readonly PatientTransactionHandler _previous;
    private readonly ILogger<ImagingStudyTransactionHandler> _logger;

    public ImagingStudyTransactionHandler(
        FhirClient client,
        PatientTransactionHandler previous,
        ILogger<ImagingStudyTransactionHandler> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _previous = previous ?? throw new ArgumentNullException(nameof(previous));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<TransactionBuilder> ConfigureAsync(
        TransactionBuilder builder,
        DicomSopInstanceEvent instanceEvent,
        CancellationToken cancellationToken = default)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        if (instanceEvent is null)
            throw new ArgumentNullException(nameof(instanceEvent));

        PatientTransactionHandler.TransactionContext context = await _previous.ConfigureAsync(builder, instanceEvent.Dataset, cancellationToken);

        Identifier imagingStudyIdentifier = dataset.GetImagingStudyIdentifier();
        ImagingStudy? imagingStudy = await GetImagingStudyOrDefaultAsync(imagingStudyIdentifier, cancellationToken);
        if (imagingStudy is null)
        {
            imagingStudy = new()
            {
                Identifier = new List<Identifier> { imagingStudyIdentifier },
                Meta = new Meta { Source = context.Endpoint.Address },
                Status = ImagingStudy.ImagingStudyStatus.Available,
                Subject = new ResourceReference($"{ResourceType.Patient:G}/{context.Patient.Id}"),
            };

            imagingStudy = UpdateImagingStudy(context, imagingStudy, dataset);
        }
        else
        {
            imagingStudy = UpdateImagingStudy(context, imagingStudy, dataset);
        }

        return builder;
    }

    private async ValueTask<ImagingStudy?> GetImagingStudyOrDefaultAsync(Identifier imagingStudyIdentifier, CancellationToken cancellationToken)
    {
        SearchParams parameters = new SearchParams()
            .Add("identifier", $"{imagingStudyIdentifier.System}|{imagingStudyIdentifier.Value}")
            .LimitTo(1);

        Bundle? bundle = await _client.SearchAsync<ImagingStudy>(parameters, cancellationToken);
        if (bundle is null)
            return null;

        return await bundle
            .GetEntriesAsync(_client)
            .Select(x => x.Resource)
            .Cast<ImagingStudy>()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private ImagingStudy UpdateImagingStudy(PatientTransactionContext context, ImagingStudy imagingStudy, DicomDataset dataset)
    {
        // Update the study date
        if (dataset.TryGetDateTimeOffset(DicomTag.StudyDate, DicomTag.StudyTime, out DateTimeOffset studyDate))
            imagingStudy.StartedElement = new FhirDateTime(studyDate);

        // Does the imaging study reference this endpoint?
        if (!imagingStudy.Endpoint.Any(e => context.Endpoint.Identifier.Any(e.IsExactly)))
            imagingStudy.Endpoint.Add(new ResourceReference($"{ResourceType.Endpoint:G}/{context.Endpoint.Id}"));

        // Update modalities
        if (dataset.TryGetSingleValue(DicomTag.Modality, out string? modality) && modality is not null)
        {
            // Create a set of all of the modalities in the DICOM study
            HashSet<string> modalities = new(StringComparer.OrdinalIgnoreCase) { modality };
            if (dataset.TryGetValues(DicomTag.ModalitiesInStudy, out string[] modalitiesInStudy))
            {
                foreach (string m in modalitiesInStudy)
                    _ = modalities.Add(m);
            }

            // Add all of the modalities that are not already present in the FHIR ImagingStudy resource
            foreach (Coding c in imagingStudy.Modality.SelectMany(m => m.Coding))
                _ = modalities.Remove(c.Code);

            foreach (string code in modalities)
                imagingStudy.Modality.Add(new CodeableConcept("http://dicom.nema.org/resources/ontology/DCM", code));
        }

        // Update notes
        if (dataset.TryGetSingleValue(DicomTag.StudyDescription, out string? description) &&
            description is not null &&
            !imagingStudy.Note.Any(note => string.Equals(note.Text, description, StringComparison.Ordinal)))
        {
            imagingStudy.Note.Add(new Annotation { Text = description });
        }

        // Update Identifiers based on Accession Number
        if (dataset.TryGetSingleValue(DicomTag.AccessionNumber, out string? accessionNumber) && accessionNumber is not null)
        {
            Identifier accessionNumberId = new(null!, accessionNumber)
            {
                Type = new CodeableConcept("http://terminology.hl7.org/CodeSystem/v2-0203", "ACSN"),
            };

            if (!imagingStudy.Identifier.Any(accessionNumberId.IsExactly))
                imagingStudy.Identifier.Add(accessionNumberId);
        }

        return imagingStudy;
    }

    private ImagingStudy AddSopInstance(ImagingStudy imagingStudy, DicomDataset dataset)
    { }

    private ImagingStudy RemoveSopInstance(ImagingStudy imagingStudy, DicomDataset dataset)
    {

    }

    internal class TransactionContext : PatientTransactionHandler.TransactionContext
    {
        public ImagingStudy ImagingStudy { get; }

        public TransactionContext(TransactionBuilder builder, Endpoint endpoint, Patient patient, ImagingStudy imagingStudy)
            : base(builder, endpoint, patient)
            => ImagingStudy = imagingStudy ?? throw new ArgumentNullException(nameof(imagingStudy));
    }
}
