// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Azure.Messaging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azure.Health.Data.Dicom.Cast;

public class SyncObservations
{
    private readonly ILogger<SyncObservations> _logger;

    public SyncObservations(ILogger<SyncObservations> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    [Function(nameof(SyncObservations))]
    public void Run([EventGridTrigger] CloudEvent cloudEvent)
    {
        _logger.LogInformation("Event type: {type}, Event subject: {subject}", cloudEvent.Type, cloudEvent.Subject);
    }
}
