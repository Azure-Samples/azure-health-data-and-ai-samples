// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Data;
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

    public async ValueTask<ResourceTransactionBuilder<ImagingStudy>> AddOrUpdateImagingStudyAsync(
        TransactionBuilder builder,
        DicomDataset dataset,
        Endpoint endpoint,
        Patient patient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dataset);

        Identifier identifier = dataset.GetStudyInstanceIdentifier();
        ImagingStudy? study = await GetImagingStudyOrDefaultAsync(identifier, cancellationToken);
        if (study is null)
        {
            study = new()
            {
                Identifier = [identifier],
                Meta = new Meta { Source = endpoint.Address },
                Status = ImagingStudy.ImagingStudyStatus.Available,
                Subject = new ResourceReference($"{ResourceType.Patient:G}/{patient.Id}"),
            };

            study = UpdateDicomStudy(study, dataset, endpoint);
            builder = builder.Create(study, new SearchParams().Add(identifier));
        }
        else
        {
            study = UpdateDicomStudy(study, dataset, endpoint);
            builder = builder.Update(new SearchParams(), study, study.Meta.VersionId);
        }

        return builder.ForResource(study);
    }

    public async ValueTask<TransactionBuilder> UpdateOrDeleteImagingStudyAsync(TransactionBuilder builder, InstanceIdentifiers identifiers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ImagingStudy? study = await GetImagingStudyOrDefaultAsync(DicomIdentifier.FromUid(identifiers.StudyInstanceUid), cancellationToken);
        if (study is not null)
        {
            ImagingStudy.SeriesComponent? series = GetSeriesOrDefault(study, identifiers.SeriesInstanceUid);
            if (series is not null)
            {
                _ = series.Instance.RemoveAll(x => x.Uid == identifiers.SopInstanceUid);

                // Update the study, unless this was the last SOP instance for the last series. Then delete the entire study
                builder = study.Series.Count == 1 && series.Instance.Count == 0
                    ? builder.Delete(nameof(ResourceType.ImagingStudy), study.Id)
                    : builder.Update(new SearchParams(), study, study.Meta.VersionId);
            }
        }

        return builder;
    }

    private async ValueTask<ImagingStudy?> GetImagingStudyOrDefaultAsync(Identifier identifier, CancellationToken cancellationToken)
    {
        Bundle? bundle = await _client.SearchAsync<ImagingStudy>(new SearchParams().Add(identifier), cancellationToken);
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
        ImagingStudy.SeriesComponent? series = GetSeriesOrDefault(imagingStudy, seriesInstanceUid);
        if (series is null)
        {
            series = new() { Uid = seriesInstanceUid };
            imagingStudy.Series.Add(series);
        }

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
        ImagingStudy.InstanceComponent? instance = GetSopInstanceOrDefault(series, sopInstanceUid);
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

    private static ImagingStudy.SeriesComponent? GetSeriesOrDefault(ImagingStudy imagingStudy, string seriesInstanceUid)
        => imagingStudy.Series.FirstOrDefault(x => string.Equals(x.Uid, seriesInstanceUid, StringComparison.Ordinal));

    private static ImagingStudy.InstanceComponent? GetSopInstanceOrDefault(ImagingStudy.SeriesComponent series, string sopInstanceUid)
        => series.Instance.FirstOrDefault(x => string.Equals(x.Uid, sopInstanceUid, StringComparison.Ordinal));
}
