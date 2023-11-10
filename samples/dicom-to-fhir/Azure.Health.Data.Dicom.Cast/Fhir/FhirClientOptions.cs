using System;
using System.ComponentModel.DataAnnotations;
using Hl7.Fhir.Rest;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

public class FhirClientOptions : FhirClientSettings
{
    [Required]
    public string? Name { get; set; }
}
