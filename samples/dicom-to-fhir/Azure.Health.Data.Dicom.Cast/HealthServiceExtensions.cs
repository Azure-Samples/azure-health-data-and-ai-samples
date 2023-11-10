using Azure.Core;
using Azure.Health.Data.Dicom.Cast.DicomWeb;
using Azure.Health.Data.Dicom.Cast.Fhir;
using Azure.Health.Data.Dicom.Cast.Http;
using FellowOakDicom.Network.Client;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace Azure.Health.Data.Dicom.Cast;

internal static class FhirServiceExtensions
{
    public static IServiceCollection ConfigureHealthClients(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<HealthWorkspaceOptions>()
            .Bind(configuration.GetSection(HealthWorkspaceOptions.SectionName))
            .ValidateDataAnnotations();

        services
            .AddHttpClient<DicomWebClient>((sp, c) =>
            {
                HealthWorkspaceOptions options = sp.GetRequiredService<IOptions<HealthWorkspaceOptions>>().Value;
                c.BaseAddress = new Uri(options.DicomServiceUri, $"v{options.Dicom?.Version}/");
            })
            .AddHttpMessageHandler<DicomDelegatingHandler>();

        services
            .AddHttpClient<FhirClient>((sp, c) =>
            {
                HealthWorkspaceOptions options = sp.GetRequiredService<IOptions<HealthWorkspaceOptions>>().Value;
                c.BaseAddress = new Uri(options.DicomServiceUri, $"v{options.Dicom?.Version}/");
            })
            .AddHttpMessageHandler<DicomDelegatingHandler>();
            .AddScoped(s =>
            {
                HealthWorkspaceOptions options = s.GetRequiredService<IOptionsSnapshot<HealthWorkspaceOptions>>().Value;
                AzureComponentFactory factory = s.GetRequiredService<AzureComponentFactory>();
                AuthenticatedHttpMessageHandler handler = new(
                    factory,
                    s.GetRequiredService<IConfiguration>().GetSection(HealthWorkspaceOptions.SectionName),
                    options.FhirServiceUri); // Service URI is the audience for FHIR

                return new FhirClient(options.FhirServiceUri, options.Fhir, handler).WithStrictSerializer();
            });

        services
            .AddScoped(s =>
            {
                

                return new DicomClient(options.FhirServiceUri, options.Fhir, handler).WithStrictSerializer();
            });

        return services;
    }
}
