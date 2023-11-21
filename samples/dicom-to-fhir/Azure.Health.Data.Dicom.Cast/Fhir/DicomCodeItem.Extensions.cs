// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using FellowOakDicom.StructuredReport;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class DicomCodeItemExtensions
{
    public static string GetSystem(this DicomCodeItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // https://www.hl7.org/fhir/terminologies-systems.html
        return item.Scheme switch
        {
            CodeSchemes.Dcm => "http://dicom.nema.org/resources/ontology/DCM",
            CodeSchemes.Sct => "http://snomed.info/sct",
            _ => item.Scheme,
        };
    }
}
