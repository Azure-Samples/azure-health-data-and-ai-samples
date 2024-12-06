using ClientApplication.Configuration;
using Grpc.Net.Client.Configuration;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        config config = new();

        using IHost host = new HostBuilder()
            .ConfigureAppConfiguration((hostingContext, configuration) =>
            {
                configuration.Sources.Clear();

                IHostEnvironment env = hostingContext.HostingEnvironment;

                configuration
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                    .AddEnvironmentVariables("AZURE_");

                IConfigurationRoot configurationRoot = configuration.Build();

                configurationRoot.Bind(config);
            })
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services =>
            {
                services.AddSingleton(config);

             
            })
            .Build();

        await host.RunAsync();
    }

}
