// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;

namespace SMARTCustomOperations.AzureAuth.Configuration
{
    public static class GraphConfiguration
    {
        public static IServiceCollection AddMicrosoftGraphClient(this IServiceCollection services)
        {
            return services.AddMicrosoftGraphClient(_ => { });
        }

        public static IServiceCollection AddMicrosoftGraphClient(this IServiceCollection services, Action<GraphConfigurationOptions> options)
        {
            services.Configure(options);
            services.AddSingleton<MicrosoftGraphAccessTokenProvider>();
            services.AddScoped(sp =>
            {
                var provider = sp.GetRequiredService<MicrosoftGraphAccessTokenProvider>();
                var authenticationProvider = new BaseBearerTokenAuthenticationProvider(provider);
                return new GraphServiceClient(authenticationProvider);
            });
            return services;
        }
    }
}
