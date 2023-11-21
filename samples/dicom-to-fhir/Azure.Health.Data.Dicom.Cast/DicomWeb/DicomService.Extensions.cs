// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Azure.Health.Data.Dicom.Cast.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Azure.Health.Data.Dicom.Cast.DicomWeb;

internal static class DicomServiceExtensions
{
    public static IServiceCollection AddDicomWebClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services
            .AddOptions<DicomWebClientOptions>()
            .BindConfiguration(DicomWebClientOptions.SectionName)
            .ValidateDataAnnotations();

        _ = services
            .AddHttpClient<DicomWebClient>(
                (sp, c) =>
                {
                    DicomWebClientOptions options = sp.GetRequiredService<IOptionsSnapshot<DicomWebClientOptions>>().Value;
                    c.BaseAddress = options.ServiceUri;
                })
            .AddHttpMessageHandler(
                sp =>
                {
                    AzureComponentFactory factory = sp.GetRequiredService<AzureComponentFactory>();
                    IConfigurationSection section = sp.GetRequiredService<IConfiguration>().GetSection(DicomWebClientOptions.SectionName);
                    return new AuthorizationHandler(factory.CreateTokenCredential(section), DicomWebClientOptions.Audience);
                });

        return services.AddScoped<DicomWebClient>();
    }
}
