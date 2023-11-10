using Azure.Health.Data.Dicom.Cast.DependencyInjection;
using Azure.Health.Data.Dicom.Cast.DicomWeb;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(builder => builder
        .AddJsonFile("worker.json", optional: false, reloadOnChange: true))
    .ConfigureServices((context, services) => services
        
        .ConfigureFhirClient(context.Configuration)
        .AddAzureClientsCore())
    .Build();

host.Run();
