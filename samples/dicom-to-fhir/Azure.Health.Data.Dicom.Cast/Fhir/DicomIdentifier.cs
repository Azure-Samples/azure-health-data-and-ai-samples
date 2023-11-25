// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using Hl7.Fhir.Model;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class DicomIdentifier
{
    public static Identifier FromUid(string uid)
        => new("urn:dicom:uid", $"urn:oid:{uid}");
}
