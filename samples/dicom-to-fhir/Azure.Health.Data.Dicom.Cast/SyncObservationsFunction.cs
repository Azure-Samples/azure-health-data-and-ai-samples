// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azure.Health.Data.Dicom.Cast;

/// <summary>
/// Represents an Azure Function for synchronizing metadata derived from DICOM SOP instances to FHIR resources.
/// </summary>
public class SyncObservationsFunction
{
    private readonly IDicomCastClient _client;
    private readonly ILogger<SyncObservationsFunction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncObservationsFunction"/> class with a DICOMcast client.
    /// </summary>
    /// <param name="client">A client for synchronizing events with an Azure FHIR Service.</param>
    /// <param name="logger">A diagnostic logger for recording telemetry.</param>
    public SyncObservationsFunction(IDicomCastClient client, ILogger<SyncObservationsFunction> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Asynchronously synchronizes a batch of DICOM SOP instances metadata based on Azure Event Grid events.
    /// </summary>
    /// <remarks>
    /// Unrecognized event types are ignored.
    /// </remarks>
    /// <param name="cloudEvents">A batch of Azure DICOM Event Grid events.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task that represents the asynchronous synchronization operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cloudEvents"/> is <see langword="null"/>.</exception>"
    /// <exception cref="OperationCanceledException">
    /// The cancellation token was canceled. This exception is stored into the returned task.
    /// </exception>
    [Function(nameof(SyncObservationsFunction))]
    public async Task RunAsync([EventGridTrigger] CloudEvent[] cloudEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cloudEvents);

        _logger.LogInformation("Received {Count} events", cloudEvents.Length);
        foreach (CloudEvent cloudEvent in cloudEvents)
        {
            _logger.LogInformation("Processing {Type} event with ID {ID}", cloudEvent.Type, cloudEvent.Id);
            if (cloudEvent.TryGetSystemEventData(out object eventData))
            {
                if (eventData is HealthcareDicomImageCreatedEventData createEvent)
                    await _client.CreateObservationsAsync(createEvent, cancellationToken);
                else if (eventData is HealthcareDicomImageUpdatedEventData updateEvent)
                    await _client.UpdateObservationsAsync(updateEvent, cancellationToken);
                else if (eventData is HealthcareDicomImageDeletedEventData deleteEvent)
                    await _client.DeleteObservationsAsync(deleteEvent, cancellationToken);
            }
        }
    }
}
