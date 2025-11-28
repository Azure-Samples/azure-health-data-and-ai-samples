// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SMARTCustomOperations.Export.Configuration;
using SMARTCustomOperations.Export.Filters;
using SMARTCustomOperations.Export.Services;

namespace SMARTCustomOperations.Export.UnitTests.Filters
{
    public class GetExportFileInputFilterTests
    {
        private static readonly ExportCustomOperationsConfig _config = new()
        {
            ApiManagementHostName = "apim-name.azure-api.net",
            ApiManagementFhirPrefex = "smart",
            FhirUrl = "https://workspace-fhir.fhir.azurehealthcareapis.com",
            ExportStorageAccountUrl = "https://account.blob.core.windows.net",
        };

        private static readonly IExportFileService _exportFileService = Substitute.For<IExportFileService>();

        private static readonly ILogger<GetExportFileInputFilter> _logger = Substitute.For<ILogger<GetExportFileInputFilter>>();

        [Fact]
        public async Task GivenAGetExportCheckOperation_WhenAccessingAllowedFile_FileObjectIsReturnedCorrectly()
        {
            string oid = Guid.NewGuid().ToString();
            string restOfPath = "DateTimeFolder/filename.ndjson";

            OperationContext context = new()
            {
                Request = new HttpRequestMessage(HttpMethod.Get, $"https://{_config.ApiManagementHostName}/{_config.ApiManagementFhirPrefex}/_export/{oid}/{restOfPath}")
            };
            context.Properties["PipelineType"] = ExportOperationType.GetExportFile.ToString();
            context.Properties["oid"] = oid;

            byte[] content = new byte[] { 0x01, 0x02, 0x03 };
            _exportFileService.GetContent(oid, restOfPath).Returns(content);

            GetExportFileInputFilter filter = new(_config, _exportFileService, _logger);
            OperationContext newContext = await filter.ExecuteAsync(context);


            Assert.Equal(content, newContext.Content);
        }

        [Fact]
        public async Task GivenAGetExportCheckOperation_WhenAccesingAnotherUserExport_ReturnsUnauthorized()
        {
            string oid = Guid.NewGuid().ToString();
            string container = Guid.NewGuid().ToString();
            string restOfPath = "DateTimeFolder/filename.ndjson";

            OperationContext context = new()
            {
                Request = new HttpRequestMessage(HttpMethod.Get, $"https://{_config.ApiManagementHostName}/{_config.ApiManagementFhirPrefex}/_export/{container}/{restOfPath}")
            };
            context.Properties["PipelineType"] = ExportOperationType.GetExportFile.ToString();
            context.Properties["oid"] = oid;

            GetExportFileInputFilter filter = new(_config, _exportFileService, _logger);

            bool trigger = false;
            filter.OnFilterError += (object? sender, FilterErrorEventArgs args) =>
            {
                trigger = true;
                Assert.Equal(nameof(GetExportFileInputFilter), args.Name);
                Assert.Equal(filter.Id, args.Id);
                Assert.True(args.IsFatal);
                Assert.Equal(HttpStatusCode.Unauthorized, args.Code);
            };

            OperationContext newContext = await filter.ExecuteAsync(context);

            // Ensure error was triggered
            Assert.True(trigger);
        }
    }
}
