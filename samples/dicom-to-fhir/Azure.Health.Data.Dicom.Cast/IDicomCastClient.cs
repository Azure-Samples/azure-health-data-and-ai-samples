// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.EventGrid.SystemEvents;
using Hl7.Fhir.Model;

namespace Azure.Health.Data.Dicom.Cast;

/// <summary>
/// Represents a client that "casts" DICOM metadata into an Azure FHIR service as FHIR resources.
/// </summary>
public interface IDicomCastClient
{
    /// <summary>
    /// Asynchronously creates new FHIR <see cref="Observation"/> resources, and other related resources, based on a newly added DICOM SOP instance.
    /// </summary>
    /// <param name="eventData">An Azure Event Grid event for the creation of a DICOM SOP instance.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task that represents the asynchronous create operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventData"/> is <see langword="null"/>.</exception>"
    /// <exception cref="OperationCanceledException">
    /// The cancellation token was canceled. This exception is stored into the returned task.
    /// </exception>
    ValueTask CreateObservationsAsync(HealthcareDicomImageCreatedEventData eventData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates FHIR <see cref="Observation"/> resources, and other related resources, based on an updated DICOM SOP instance.
    /// </summary>
    /// <param name="eventData">An Azure Event Grid event for the update of a DICOM SOP instance.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventData"/> is <see langword="null"/>.</exception>"
    /// <exception cref="OperationCanceledException">
    /// The cancellation token was canceled. This exception is stored into the returned task.
    /// </exception>
    ValueTask UpdateObservationsAsync(HealthcareDicomImageUpdatedEventData eventData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes FHIR <see cref="Observation"/> resources, and other related resources, based on a deleted DICOM SOP instance.
    /// </summary>
    /// <param name="eventData">An Azure Event Grid event for the deletion of a DICOM SOP instance.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventData"/> is <see langword="null"/>.</exception>"
    /// <exception cref="OperationCanceledException">
    /// The cancellation token was canceled. This exception is stored into the returned task.
    /// </exception>
    ValueTask DeleteObservationsAsync(HealthcareDicomImageDeletedEventData eventData, CancellationToken cancellationToken = default);
}
