// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FellowOakDicom;
using Hl7.Fhir.Model;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class DicomDatasetExtensions
{
    public static Identifier GetPatientIdentifier(this DicomDataset dataset)
    {
        if (dataset is null)
            throw new ArgumentNullException(nameof(dataset));

        if (!dataset.TryGetSingleValue(DicomTag.IssuerOfPatientID, out string issuer))
            issuer = string.Empty;

        if (!dataset.TryGetSingleValue(DicomTag.PatientID, out string patientId))
            throw new KeyNotFoundException(Exceptions.PatientIdNotFound);

        return new(issuer, patientId);
    }

    public static Identifier GetImagingStudyIdentifier(this DicomDataset dataset)
    {
        if (dataset is null)
            throw new ArgumentNullException(nameof(dataset));

        if (!dataset.TryGetSingleValue(DicomTag.StudyInstanceUID, out string studyInstanceUID))
            throw new KeyNotFoundException(Exceptions.StudyInstanceUidNotFound);

        return new Identifier("urn:dicom:uid", $"urn:oid:{studyInstanceUID}");
    }

    public static bool TryGetDateTimeOffset(this DicomDataset dataset, DicomTag dateTag, DicomTag timeTag, out DateTimeOffset value)
    {
        if (dataset is null)
            throw new ArgumentNullException(nameof(dataset));

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
