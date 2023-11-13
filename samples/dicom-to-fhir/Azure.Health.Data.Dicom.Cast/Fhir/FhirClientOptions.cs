// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel.DataAnnotations;
using Hl7.Fhir.Rest;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

public class FhirClientOptions : FhirClientSettings
{
    public const string SectionName = "Fhir";

    [Required]
    public string? Name { get; set; }

    [Required]
    public string? Workspace { get; set; }

    [Required]
    public string? Service { get; set; }

    public string? Credential { get; set; }

    public string? ClientId { get; set; }

    public Uri ServiceUri => new($"https://{Name}-{Service}.fhir.azurehealthcareapis.com", UriKind.Absolute);
}
