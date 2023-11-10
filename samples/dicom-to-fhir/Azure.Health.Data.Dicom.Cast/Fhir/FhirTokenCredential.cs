using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

// TODO: Use named services in .NET 8
public class FhirTokenCredential
{
    private readonly string _scope;
    private readonly TokenCredential _tokenCredential;

    public FhirTokenCredential(AzureComponentFactory factory, IConfiguration configuration, IOptions<HealthWorkspaceOptions> options)
    {
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        if (configuration is null)
            throw new ArgumentNullException(nameof(configuration));

        if (options?.Value.Fhir is null)
            throw new ArgumentNullException(nameof(options));

        IConfigurationSection dicomSection = configuration.GetSection(HealthWorkspaceOptions.SectionName + ":" + nameof(HealthWorkspaceOptions.Dicom));
        _tokenCredential = factory.CreateTokenCredential(dicomSection);
        _scope = options.Value.FhirServiceUri.AbsoluteUri;
    }

    public AccessToken GetToken(CancellationToken cancellationToken)
        => _tokenCredential.GetToken(new TokenRequestContext(new[] { _scope }), cancellationToken);

    public ValueTask<AccessToken> GetTokenAsync(CancellationToken cancellationToken)
        => _tokenCredential.GetTokenAsync(new TokenRequestContext(new[] { _scope }), cancellationToken);
}
