// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class SearchParamsExtensions
{
    public static SearchParams Add(this SearchParams searchParams, Identifier identifier)
    {
        ArgumentNullException.ThrowIfNull(searchParams);
        ArgumentNullException.ThrowIfNull(identifier);

        return searchParams.Add("identifier", $"{identifier.System}|{identifier.Value}");
    }
}
