using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;

namespace SMARTCustomOperations.AzureAuth.Configuration
{
    public static class GraphConfiguration
    {
        public static IServiceCollection AddMicrosoftGraphClient(this IServiceCollection services)
        {
            return services.AddMicrosoftGraphClient(options =>
            {
                options = new();
            });
        }

        public static IServiceCollection AddMicrosoftGraphClient(this IServiceCollection services, Action<GraphConfigurationOptions> options)
        {
            services.Add(new ServiceDescriptor(typeof(MicrosoftGraphAccessTokenProvider), typeof(MicrosoftGraphAccessTokenProvider), ServiceLifetime.Singleton));
            services.Configure<GraphConfigurationOptions>(options);

            services.AddScoped(sp =>
            {
                var tokenAcquisition = sp.GetRequiredService<MicrosoftGraphAccessTokenProvider>();
                var authenticationProvider = new BaseBearerTokenAuthenticationProvider(tokenAcquisition);
                var graphServiceClient = new GraphServiceClient(authenticationProvider);
                return graphServiceClient;
            });

            return services;
        }
    }
}
