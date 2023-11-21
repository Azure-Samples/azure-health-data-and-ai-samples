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

        if (!dataset.TryGetSingleValue(DicomTag.PatientID, out string patientId))
            throw new KeyNotFoundException(Exceptions.PatientIdNotFound);

        return new(issuer, patientId);
    }

    public static Identifier GetImagingStudyIdentifier(this DicomDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        if (!dataset.TryGetSingleValue(DicomTag.StudyInstanceUID, out string studyInstanceUID))
            throw new KeyNotFoundException(Exceptions.StudyInstanceUidNotFound);

        return new Identifier("urn:dicom:uid", $"urn:oid:{studyInstanceUID}");
    }

    public static bool TryGetDateTimeOffset(this DicomDataset dataset, DicomTag dateTag, DicomTag timeTag, out DateTimeOffset value)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        if (dataset.TryGetSingleValue(dateTag, out DateTime date) && dataset.TryGetSingleValue(timeTag, out DateTime time))
        {
            if (date != default || time != default)
            {
                // Assume UTC if no offset is specified. Local timezone is too ambiguous for cloud service
                TimeSpan offset = TryParseUtcOffset(dataset, out TimeSpan offsetValue) ? offsetValue : TimeSpan.Zero;
                value = new(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, time.Millisecond, offset);
                return true;
            }
        }

        value = default;
        return false;
    }

    public static bool TryCreateIrradiationEvent(
        this DicomDataset dataset,
        ResourceReference patientReference,
        [NotNullWhen(true)] out Observation? observation)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(patientReference);

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
            Identifier = { dataset.GetImagingStudyIdentifier() },
            Status = ObservationStatus.Preliminary,
            Subject = patientReference,
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

    public static bool TryCreateDoseSummary(
        this DicomDataset dataset,
        DicomCodeItem code,
        ResourceReference patientReference,
        ResourceReference imagingStudyReference,
        [NotNullWhen(true)] out Observation? observation)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(patientReference);
        ArgumentNullException.ThrowIfNull(imagingStudyReference);

        if (!code.Equals(StructuredReportCodes.RadiopharmaceuticalRadiationDoseReport) && !code.Equals(StructuredReportCodes.XRayRadiationDoseReport))
        {
            observation = default;
            return false;
        }

        DicomStructuredReport report = new(dataset);

        // Create the observation
        observation = new Observation
        {
            Code = FhirObservationCodes.RadiationExposure,
            Identifier = { dataset.GetImagingStudyIdentifier() },
            Status = ObservationStatus.Preliminary,
            Subject = patientReference,
            PartOf = { imagingStudyReference },
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
