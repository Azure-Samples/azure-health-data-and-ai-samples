// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Utility;
using Hl7.FhirPath.Sprache;
using Microsoft.Extensions.Logging;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal class PatientTransactionHandler
{
    private readonly FhirClient _client;
    private readonly ILogger<PatientTransactionHandler> _logger;

    public PatientTransactionHandler(FhirClient client, ILogger<PatientTransactionHandler> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<Patient> AddOrUpdatePatientAsync(
        TransactionBuilder builder,
        DicomDataset dataset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dataset);

        Identifier identifier = dataset.GetPatientIdentifier();
        Patient? patient = await GetPatientOrDefaultAsync(identifier, cancellationToken);

        if (patient is null)
        {
            patient = new()
            {
                Id = $"urn:uuid:{Guid.NewGuid()}",
                Identifier = { identifier },
            };

            patient = UpdatePatient(patient, dataset);

            SearchParams ifNotExistsCondition = new SearchParams().Add("identifier", $"{identifier.System}|{identifier.Value}");
            _ = builder.Create(patient, ifNotExistsCondition);
        }
        else
        {
            patient = UpdatePatient(patient, dataset);

            _ = builder.Update(new SearchParams(), patient, patient.Meta.VersionId);
        }

        return patient;
    }

    private async ValueTask<Patient?> GetPatientOrDefaultAsync(Identifier patientId, CancellationToken cancellationToken)
    {
        SearchParams parameters = new SearchParams()
            .Add("identifier", $"{patientId.System}|{patientId.Value}")
            .LimitTo(1);

        Bundle? bundle = await _client.SearchAsync<Patient>(parameters, cancellationToken);
        if (bundle is null)
            return null;

        return await bundle
            .GetEntriesAsync(_client)
            .Select(x => x.Resource)
            .Cast<Patient>()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static Patient UpdatePatient(Patient patient, DicomDataset dataset)
    {
        // Update Patient Gender
        if (TryParsePatientGender(dataset, out AdministrativeGender? gender))
            patient.Gender = gender;

        // Update Patient Birth Date
        if (TryParsePatientBirthDate(dataset, out Date? birthDate))
            patient.BirthDateElement = birthDate;

        // Update the patient's usual name
        _ = UpdatePatientName(patient, dataset);

        return patient;
    }

    private static bool TryParsePatientGender(DicomDataset dataset, [NotNullWhen(true)] out AdministrativeGender? gender)
    {
        if (!dataset.TryGetString(DicomTag.PatientSex, out string patientSex) || string.IsNullOrWhiteSpace(patientSex))
        {
            gender = default;
            return false;
        }

        gender = patientSex switch
        {
            "M" => AdministrativeGender.Male,
            "F" => AdministrativeGender.Female,
            "O" => AdministrativeGender.Other,
            _ => throw new FormatException(string.Format(CultureInfo.CurrentCulture, Exceptions.InvalidPatientSexFormat, patientSex)),
        };

        return true;
    }

    private static bool TryParsePatientBirthDate(DicomDataset dataset, [NotNullWhen(true)] out Date? birthDate)
    {
        if (dataset.TryGetSingleValue(DicomTag.PatientBirthDate, out DateTime patientBirthDate) && patientBirthDate != default)
        {
            Date fhirDate = new(patientBirthDate.Year, patientBirthDate.Month, patientBirthDate.Day);
            if (Date.IsValidValue(fhirDate.Value))
            {
                birthDate = fhirDate;
                return true;
            }
        }

        birthDate = default;
        return false;
    }

    private static HumanName UpdatePatientName(Patient patient, DicomDataset dataset)
    {
        HumanName? name = patient
            .Name
            .FirstOrDefault(n => n.Use == HumanName.NameUse.Usual) ?? new() { Use = HumanName.NameUse.Usual };

        if (dataset.TryGetString(DicomTag.PatientName, out string patientName) && !string.IsNullOrWhiteSpace(patientName))
        {
            // Refer to PS3.5 6.2 and 6.2.1 for parsing logic
            string[] parts = patientName.Trim().Split('^');
            name.Family = parts[0];

            List<string> combinedGivenNames = new();

            // Given name
            if (TryGetNamePart(parts, 1, out string[]? givenNames))
                combinedGivenNames.AddRange(givenNames);

            // Middle name
            if (TryGetNamePart(parts, 2, out string[]? middleNames))
                combinedGivenNames.AddRange(middleNames);

            name.Given = combinedGivenNames;

            // Prefix
            if (TryGetNamePart(parts, 3, out string[]? prefixes))
                name.Prefix = prefixes;

            // Suffix
            if (TryGetNamePart(parts, 4, out string[]? suffixes))
                name.Suffix = suffixes;
        }

        return name;

        static bool TryGetNamePart(string[] parts, int index, [NotNullWhen(true)] out string[]? nameParts)
        {
            if (parts.Length > index && !string.IsNullOrWhiteSpace(parts[index]))
            {
                nameParts = parts[index].Split(' ');
                return true;
            }

            nameParts = default;
            return false;
        }
    }
}
