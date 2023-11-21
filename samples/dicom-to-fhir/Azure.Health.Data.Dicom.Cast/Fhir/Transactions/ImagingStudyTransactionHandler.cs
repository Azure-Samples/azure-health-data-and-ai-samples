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
    private readonly ILogger<ImagingStudyTransactionHandler> _logger;

    public ImagingStudyTransactionHandler(FhirClient client, ILogger<ImagingStudyTransactionHandler> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<ImagingStudy> AddOrUpdateImagingStudyAsync(
        TransactionBuilder builder,
        DicomDataset dataset,
        Endpoint endpoint,
        Patient patient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dataset);

        Identifier identifier = dataset.GetImagingStudyIdentifier();
        ImagingStudy? imagingStudy = await GetImagingStudyOrDefaultAsync(identifier, cancellationToken);
        if (imagingStudy is null)
        {
            imagingStudy = new()
            {
                Identifier = new List<Identifier> { identifier },
                Meta = new Meta { Source = endpoint.Address },
                Status = ImagingStudy.ImagingStudyStatus.Available,
                Subject = new ResourceReference($"{ResourceType.Patient:G}/{patient.Id}"),
            };

            imagingStudy = UpdateDicomStudy(imagingStudy, dataset, endpoint);

            SearchParams ifNoneExistsCondition = new SearchParams().Add("identifier", $"{identifier.System}|{identifier.Value}");
            _ = builder.Create(imagingStudy, ifNoneExistsCondition);
        }
        else
        {
            imagingStudy = UpdateDicomStudy(imagingStudy, dataset, endpoint);
            _ = builder.Update(new SearchParams(), imagingStudy, imagingStudy.Meta.VersionId);
        }

        return imagingStudy;
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

    private static ImagingStudy UpdateDicomStudy(ImagingStudy imagingStudy, DicomDataset dataset, Endpoint endpoint)
    {
        // Update Study Date
        if (dataset.TryGetDateTimeOffset(DicomTag.StudyDate, DicomTag.StudyTime, out DateTimeOffset studyDate))
            imagingStudy.StartedElement = new FhirDateTime(studyDate);

        // Does the imaging study reference this endpoint?
        if (!imagingStudy.Endpoint.Any(e => endpoint.Identifier.Any(e.IsExactly)))
            imagingStudy.Endpoint.Add(new ResourceReference($"{ResourceType.Endpoint:G}/{endpoint.Id}"));

        // Update Modalities
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

        // Update Notes
        if (dataset.TryGetSingleValue(DicomTag.StudyDescription, out string? description) &&
            !string.IsNullOrWhiteSpace(description) &&
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

        // Update the series
        _ = AddOrUpdateSeries(imagingStudy, dataset);

        return imagingStudy;
    }

    private static ImagingStudy.SeriesComponent AddOrUpdateSeries(ImagingStudy imagingStudy, DicomDataset dataset)
    {
        // Find Series
        string seriesInstanceUid = dataset.GetSingleValue<string>(DicomTag.SeriesInstanceUID);
        ImagingStudy.SeriesComponent series = imagingStudy
            .Series
            .FirstOrDefault(x => string.Equals(x.Uid, seriesInstanceUid, StringComparison.Ordinal)) ?? new() { Uid = seriesInstanceUid };

        // Update Series Number
        if (dataset.TryGetSingleValue(DicomTag.SeriesNumber, out int seriesNumber))
            series.Number = seriesNumber;

        // Update Description
        if (dataset.TryGetSingleValue(DicomTag.SeriesDescription, out string description) && !string.IsNullOrWhiteSpace(description))
            series.Description = description;

        // Update Modality
        if (dataset.TryGetSingleValue(DicomTag.Modality, out string? modality) && !string.IsNullOrWhiteSpace(modality))
            series.Modality = new CodeableConcept("http://dicom.nema.org/resources/ontology/DCM", modality);

        // Update Study Date
        if (dataset.TryGetDateTimeOffset(DicomTag.StudyDate, DicomTag.StudyTime, out DateTimeOffset studyDate))
            series.StartedElement = new FhirDateTime(studyDate);

        // Update the SOP instance
        _ = AddOrUpdateSopInstance(series, dataset);

        return series;
    }

    private static ImagingStudy.InstanceComponent AddOrUpdateSopInstance(ImagingStudy.SeriesComponent series, DicomDataset dataset)
    {
        // Find SOP Instance
        string sopInstanceUid = dataset.GetSingleValue<string>(DicomTag.SOPInstanceUID);
        ImagingStudy.InstanceComponent? instance = series.Instance.FirstOrDefault(x => string.Equals(x.Uid, sopInstanceUid, StringComparison.Ordinal));
        if (instance is null)
        {
            instance = new() { Uid = sopInstanceUid };
            series.Instance.Add(instance);
        }

        // Update SOP Class
        if (dataset.TryGetSingleValue(DicomTag.SOPClassUID, out string? sopClassUid) && !string.IsNullOrWhiteSpace(sopClassUid))
            instance.SopClass = new Coding("urn:ietf:rfc:3986", $"urn:oid:{sopClassUid}");

        // Update Instance Number
        if (dataset.TryGetSingleValue(DicomTag.InstanceNumber, out int instanceNumber))
            instance.Number = instanceNumber;

        return instance;
    }
}
