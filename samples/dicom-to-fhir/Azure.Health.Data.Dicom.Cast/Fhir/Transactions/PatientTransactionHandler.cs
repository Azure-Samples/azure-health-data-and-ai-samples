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
    private readonly EndpointTransactionHandler _previous;
    private readonly ILogger<PatientTransactionHandler> _logger;

    public PatientTransactionHandler(
        FhirClient client,
        EndpointTransactionHandler previous,
        ILogger<PatientTransactionHandler> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _previous = previous ?? throw new ArgumentNullException(nameof(previous));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<TransactionContext> ConfigureAsync(
        TransactionBuilder builder,
        DicomDataset dataset,
        CancellationToken cancellationToken = default)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        if (dataset is null)
            throw new ArgumentNullException(nameof(dataset));

        EndpointTransactionHandler.TransactionContext context = await _previous.ConfigureAsync(builder, dataset, cancellationToken);

        Identifier fhirPatientId = dataset.GetPatientIdentifier();
        Patient? patient = await GetPatientOrDefaultAsync(fhirPatientId, cancellationToken);

        // Do not update patient if it already exists in the FHIR server
        if (patient is null)
        {
            SearchParams ifNotExistsCondition = new SearchParams().Add("identifier", $"{fhirPatientId.System}|{fhirPatientId.Value}");
            patient = ParsePatient(fhirPatientId, dataset);
            builder = builder.Create(patient, ifNotExistsCondition);
        }

        return new TransactionContext(builder, context.Endpoint, patient);
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

    private static Patient ParsePatient(Identifier patientId, DicomDataset dataset)
    {
        Patient patient = new()
        {
            Id = $"urn:uuid:{Guid.NewGuid()}",
            Identifier = { patientId },
        };

        if (TryParsePatientName(dataset, out HumanName? name))
            patient.Name.Add(name);

        if (TryParsePatientGender(dataset, out AdministrativeGender? gender))
            patient.Gender = gender;

        if (TryParsePatientBirthDate(dataset, out Date? birthDate))
            patient.BirthDateElement = birthDate;

        return patient;
    }

    private static bool TryParsePatientName(DicomDataset dataset, [NotNullWhen(true)] out HumanName? name)
    {
        if (!dataset.TryGetString(DicomTag.PatientName, out string patientName) || string.IsNullOrWhiteSpace(patientName))
        {
            name = default;
            return false;
        }

        // Refer to PS3.5 6.2 and 6.2.1 for parsing logic
        string[] parts = patientName.Trim().Split('^');

        name = new()
        {
            Use = HumanName.NameUse.Usual,
            Family = parts[0]
        };

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

        return true;

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

    public class TransactionContext : EndpointTransactionHandler.TransactionContext
    {
        public Patient Patient { get; }

        public TransactionContext(TransactionBuilder builder, Endpoint endpoint, Patient patient)
            : base(builder, endpoint)
            => Patient = patient ?? throw new ArgumentNullException(nameof(patient));
    }
}
