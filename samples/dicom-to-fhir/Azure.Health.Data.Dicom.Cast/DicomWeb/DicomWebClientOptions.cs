// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel.DataAnnotations;

namespace Azure.Health.Data.Dicom.Cast.DicomWeb;

public class DicomWebClientOptions : FellowOakDicom.Network.Client.DicomClientOptions
{
    public const string SectionName = "Dicom";

    public static readonly Uri Audience = new("https://dicom.healthcareapis.azure.com", UriKind.Absolute);

    [Required]
    public string? Workspace { get; set; }

    [Required]
    public string? Service { get; set; }

    [Range(1, int.MaxValue)] // At the time of writing, only 1 and 2 are supported
    public int Version { get; set; } = 2;

    public string? Credential { get; set; }

    public string? ClientId { get; set; }

    public Uri ServiceUri => new($"https://{Workspace}-{Service}.dicom.azurehealthcareapis.com/v{Version}/", UriKind.Absolute);
}
