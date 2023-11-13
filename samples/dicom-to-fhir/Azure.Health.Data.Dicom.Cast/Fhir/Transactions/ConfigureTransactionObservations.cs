// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal class ConfigureTransactionObservations
{
    private readonly ImagingStudyTransactionHandler _configureStudy;
    private readonly FhirClient _client;
    private readonly ILogger<ImagingStudyTransactionHandler> _logger;

    public ConfigureTransactionObservations(
        FhirClient client,
        ImagingStudyTransactionHandler configureStudy,
        ILogger<ImagingStudyTransactionHandler> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
