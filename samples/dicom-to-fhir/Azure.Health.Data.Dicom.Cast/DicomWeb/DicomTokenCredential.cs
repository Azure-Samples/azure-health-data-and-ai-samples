using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace Azure.Health.Data.Dicom.Cast.DicomWeb;

// TODO: Use named services in .NET 8
public class DicomTokenCredential
{
    private static readonly string Scope = DicomClientOptions.Audience.AbsoluteUri;

    private readonly TokenCredential _tokenCredential;

    public DicomTokenCredential(AzureComponentFactory factory, IConfiguration configuration)
    {
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        if (configuration is null)
            throw new ArgumentNullException(nameof(configuration));

        IConfigurationSection dicomSection = configuration.GetSection(HealthWorkspaceOptions.SectionName + ":" + nameof(HealthWorkspaceOptions.Dicom));
        _tokenCredential = factory.CreateTokenCredential(dicomSection);
    }

    public AccessToken GetToken(CancellationToken cancellationToken)
        => _tokenCredential.GetToken(new TokenRequestContext(new[] { Scope }), cancellationToken);

    public ValueTask<AccessToken> GetTokenAsync(CancellationToken cancellationToken)
        => _tokenCredential.GetTokenAsync(new TokenRequestContext(new[] { Scope }), cancellationToken);
}
