// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Health.Data.Dicom.Cast;
using Azure.Health.Data.Dicom.Cast.DicomWeb;
using Azure.Health.Data.Dicom.Cast.Fhir;
using Azure.Health.Data.Dicom.Cast.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(builder => builder
        .AddJsonFile("worker.json", optional: false, reloadOnChange: true))
    .ConfigureServices((context, services) => services
        .AddSingleton<IDicomCastClient, ExampleDicomCastClient>()
        .AddJsonSerialization()
        .AddDicomWebClient()
        .AddFhirClient()
        .AddFhirTransactionHandlers()
        .AddAzureClientsCore())
    .Build();

host.Run();
