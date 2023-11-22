// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Hl7.Fhir.Model;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class ResourceExtensions
{
    public static ResourceReference GetReference(this Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return new ResourceReference($"{resource.TypeName}/{resource.Id}");
    }
}
