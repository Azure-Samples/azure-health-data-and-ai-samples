// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Core;
using Azure.Identity;

namespace SMARTCustomOperations.AzureAuth.Configuration
{
    public class GraphConfigurationOptions
    {
        public TokenCredential Credential { get; set; } = new DefaultAzureCredential();

        public Uri AuthBaseUri { get; set; } = new Uri("https://graph.microsoft.com");

        public string[] Scopes { get; set; } = new[] { "https://graph.microsoft.com/.default" };
    }
}
