// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace SMARTCustomOperations.AzureAuth.Models
{
    /// <summary>
    /// Marker stored in the shared cache to indicate that an access token (identified by its unique
    /// token id) has been revoked. Presence of the entry = blocked.
    /// </summary>
    public class BlockedTokenCacheObject
    {
        [JsonPropertyName("blocked")]
        public bool Blocked { get; set; } = true;

        [JsonPropertyName("blockedUtc")]
        public DateTimeOffset BlockedUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
