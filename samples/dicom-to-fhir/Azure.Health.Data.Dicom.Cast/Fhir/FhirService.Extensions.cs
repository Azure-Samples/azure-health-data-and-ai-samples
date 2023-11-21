// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Azure.Health.Data.Dicom.Cast.Http;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class FhirServiceExtensions
{
    public static IServiceCollection AddFhirClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services
            .AddOptions<FhirClientOptions>()
            .BindConfiguration(FhirClientOptions.SectionName)
            .ValidateDataAnnotations();

        _ = services
            .AddHttpClient<FhirClient>()
            .AddHttpMessageHandler(
                sp =>
                {
                    AzureComponentFactory factory = sp.GetRequiredService<AzureComponentFactory>();
                    IConfigurationSection section = sp.GetRequiredService<IConfiguration>().GetSection(FhirClientOptions.SectionName);
                    FhirClientOptions options = sp.GetRequiredService<IOptionsSnapshot<FhirClientOptions>>().Value;
                    return new AuthorizationHandler(factory.CreateTokenCredential(section), options.ServiceUri);
                });

        return services.AddScoped(
            sp =>
            {
                FhirClientOptions options = sp.GetRequiredService<IOptionsSnapshot<FhirClientOptions>>().Value;
                HttpClient httpClient = sp.GetRequiredService<HttpClient>();
                return new FhirClient(options.ServiceUri, httpClient, options);
            });
    }
}
