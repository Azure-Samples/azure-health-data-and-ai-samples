// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FellowOakDicom;
using FellowOakDicom.StructuredReport;
using Hl7.Fhir.Model;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class DicomDatasetExtensions
{
    public static Identifier GetPatientIdentifier(this DicomDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        if (!dataset.TryGetSingleValue(DicomTag.IssuerOfPatientID, out string issuer))
            issuer = string.Empty;

        if (!dataset.TryGetPatientId(out string? patientId))
            throw new KeyNotFoundException(Exceptions.PatientIdNotFound);

        return new(issuer, patientId);
    }

    public static Identifier GetSopInstanceIdentifier(this DicomDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        if (!dataset.TryGetSingleValue(DicomTag.SOPInstanceUID, out string? sopInstanceUid) || string.IsNullOrWhiteSpace(sopInstanceUid))
            throw new KeyNotFoundException(Exceptions.StudyInstanceUidNotFound);

        return DicomIdentifier.FromUid(sopInstanceUid);
    }

    public static Identifier GetStudyInstanceIdentifier(this DicomDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        if (!dataset.TryGetSingleValue(DicomTag.StudyInstanceUID, out string? studyInstanceUID) || string.IsNullOrWhiteSpace(studyInstanceUID))
            throw new KeyNotFoundException(Exceptions.StudyInstanceUidNotFound);

        return DicomIdentifier.FromUid(studyInstanceUID);
    }

    public static bool TryCreateDoseSummary(
        this DicomDataset dataset,
        DicomCodeItem code,
        Endpoint endpoint,
        ResourceReference patient,
        ResourceReference imagingSelection,
        [NotNullWhen(true)] out Observation? observation)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(imagingSelection);

        if (!code.Equals(StructuredReportCodes.RadiopharmaceuticalRadiationDoseReport) &&
            !code.Equals(StructuredReportCodes.XRayRadiationDoseReport))
        {
            observation = default;
            return false;
        }

        DicomStructuredReport report = new(dataset);

        // Create the observation
        observation = new Observation
        {
            Code = FhirObservationCodes.RadiationExposure,
            Id = $"urn:uuid:{Guid.NewGuid()}",
            Identifier = { dataset.GetSopInstanceIdentifier() },
            Meta = new Meta { Source = endpoint.Address },
            PartOf = { imagingSelection },
            Status = ObservationStatus.Preliminary,
            Subject = patient,
        };

        // Try to get accession number from report first then tag; ignore if it is not present it is not a required identifier.
        string? accessionNumber = report.Get<string?>(StructuredReportCodes.AccessionNumber, default);
        if (string.IsNullOrEmpty(accessionNumber) && !dataset.TryGetSingleValue(DicomTag.AccessionNumber, out accessionNumber))
            accessionNumber = default;

        if (!string.IsNullOrEmpty(accessionNumber))
        {
            observation.Identifier.Add(new()
            {
                Value = accessionNumber,
                Type = FhirObservationCodes.Accession,
            });
        }

        observation.AddComponents(
            report,
            StructuredReportCodes.AccumulatedAverageGlandularDose,
            StructuredReportCodes.AcquisitionDoseAreaProductTotal,
            StructuredReportCodes.AdministeredActivity,
            StructuredReportCodes.CtDoseLengthProductTotal,
            StructuredReportCodes.DoseAreaProductTotal,
            StructuredReportCodes.DoseRpTotal,
            StructuredReportCodes.FluoroDoseAreaProductTotal,
            StructuredReportCodes.MeanCtdIvol,
            StructuredReportCodes.Radionuclide,
            StructuredReportCodes.RadiopharmaceuticalAgent,
            StructuredReportCodes.RadiopharmaceuticalVolume,
            StructuredReportCodes.RouteOfAdministration,
            StructuredReportCodes.TotalFluoroTime,
            StructuredReportCodes.TotalNumberOfIrradiationEvents,
            StructuredReportCodes.TotalNumberOfRadiographicFrames);

        return true;
    }

    public static bool TryCreateIrradiationEvent(
        this DicomDataset dataset,
        DicomCodeItem code,
        Endpoint endpoint,
        ResourceReference patient,
        ResourceReference imagingSelection,
        [NotNullWhen(true)] out Observation? observation)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(patient);

        if (!code.Equals(StructuredReportCodes.CtAcquisition) &&
            !code.Equals(StructuredReportCodes.IrradiationEventXRayData) &&
            !code.Equals(StructuredReportCodes.RadiopharmaceuticalAdministration))
        {
            observation = default;
            return false;
        }

        DicomStructuredReport report = new(dataset);

        // Try to extract the event UID
        DicomUID irradiationEventUidValue = report.Get<DicomUID>(StructuredReportCodes.IrradiationEventUid, default!); // 2nd arg is unused
        if (irradiationEventUidValue is null)
        {
            observation = default;
            return false;
        }

        // Create the observation
        observation = new Observation
        {
            Code = FhirObservationCodes.IrradiationEvent,
            Id = $"urn:uuid:{Guid.NewGuid()}",
            Identifier = { dataset.GetSopInstanceIdentifier() },
            Meta = new Meta { Source = endpoint.Address },
            PartOf = { imagingSelection },
            Status = ObservationStatus.Preliminary,
            Subject = patient,
        };

        DicomCodeItem bodySite = report.Get<DicomCodeItem>(StructuredReportCodes.TargetRegion, default!);
        if (bodySite is not null)
            observation.BodySite = new CodeableConcept(bodySite.GetSystem(), bodySite.Value, bodySite.Meaning);

        observation.AddComponents(
            report,
            StructuredReportCodes.CtdIwPhantomType,
            StructuredReportCodes.Dlp,
            StructuredReportCodes.MeanCtdIvol);

        return true;
    }

    public static bool TryGetDateTimeOffset(this DicomDataset dataset, DicomTag dateTag, DicomTag timeTag, out DateTimeOffset value)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        if (dataset.TryGetSingleValue(dateTag, out DateTime date) && dataset.TryGetSingleValue(timeTag, out DateTime time))
        {
            if (date != default || time != default)
            {
                // Assume UTC if no offset is specified. Local timezone is too ambiguous for a cloud service
                TimeSpan offset = TryParseUtcOffset(dataset, out TimeSpan offsetValue) ? offsetValue : TimeSpan.Zero;
                value = new(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, time.Millisecond, offset);
                return true;
            }
        }

        value = default;
        return false;
    }

    public static bool TryGetPatientId(this DicomDataset dataset, [NotNullWhen(true)] out string? patientId)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        if (dataset.TryGetSingleValue(DicomTag.PatientID, out patientId) && !string.IsNullOrWhiteSpace(patientId))
            return true;

        patientId = default;
        return false;
    }

    private static bool TryParseUtcOffset(this DicomDataset dataset, out TimeSpan offset)
    {
        if (dataset.TryGetSingleValue(DicomTag.TimezoneOffsetFromUTC, out string offsetString) &&
            DateTimeOffset.TryParseExact(offsetString, "zzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTimeOffset value))
        {
            offset = value.Offset;
            return true;
        }

        offset = default;
        return false;
    }
}
