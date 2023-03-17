using Azure.Storage.Blobs;
using HL7Converter.Configuration;
using HL7Converter.FhirOperation;
using HL7Converter.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;
using Response = HL7Converter.Model.Response;

namespace HL7Converter.ProcessConverter
{
    public class Converter : IConverter
    {
        public Converter(BlobConfiguration blobConfiguration, AppConfiguration appConfiguration, IFhirClient fhirClient, BlobServiceClient blobServiceClient, TelemetryClient telemetryClient = null, ILogger<Converter> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _telemetryClient = telemetryClient;
            _logger = logger;
            _blobConfiguration = blobConfiguration;
            _fhirClient = fhirClient;
            _blobServiceClient = blobServiceClient;
            _appConfiguration = appConfiguration;
        }

        private readonly string _id;
        private readonly TelemetryClient _telemetryClient;
        private readonly BlobConfiguration _blobConfiguration;
        private readonly AppConfiguration _appConfiguration;
        private readonly ILogger _logger;
        private readonly IFhirClient _fhirClient;
        private readonly BlobServiceClient _blobServiceClient;
        public string Id => _id;
        public string Name => "HL7Converter";
        private bool isFileSkipped = false;

        public async Task<string> Execute(string requestData)
        {
            DateTime start = DateTime.Now;
            Hl7ConverterResponse hl7Response = new();
            hl7Response.IsFileSkipped = false;
            string hl7ConverterRequestTemplate = "{\"parameter\":[{\"name\":\"inputData\",\"valueString\":\"\"},{\"name\":\"inputDataType\",\"valueString\":\"Hl7v2\"},{\"name\":\"templateCollectionReference\",\"valueString\":\"microsofthealth/fhirconverter:default\"},{\"name\":\"rootTemplate\",\"valueString\":\"\"}],\"resourceType\":\"Parameters\"}";
            HttpResponseMessage httpResponseMessage = new();
            try
            {
                _logger?.LogInformation($"hl7Converter Function start");

                HL7ConverterInput hL7ConverterInput = JsonConvert.DeserializeObject<HL7ConverterInput>(requestData);
                string hl7FilesArray = string.Empty;
                var hl7ArrayFileName = System.Text.Encoding.Default.GetString(Convert.FromBase64String(hL7ConverterInput.Hl7ArrayFileName));

                if(hL7ConverterInput != null && !string.IsNullOrEmpty(hl7ArrayFileName))
                {
                    var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.ValidatedContainer);
                    var blobFile = blobContainer.GetBlobClient(hl7ArrayFileName);

                    if (blobFile != null && await blobFile.ExistsAsync())
                    {
                        var blobFileData = await blobFile.OpenReadAsync();
                        using (var streamReader = new StreamReader(blobFileData))
                        {
                            hl7FilesArray = await streamReader.ReadToEndAsync();
                        }

                        blobFileData.Close();
                    }

                    if (!string.IsNullOrEmpty(hl7FilesArray))
                    {
                        List<Hl7File> hl7FilesList = JsonConvert.DeserializeObject<List<Hl7File>>(hl7FilesArray);

                        if (hl7FilesList != null && hl7FilesList.Count > 0)
                        {
                            _logger?.LogInformation($"Hl7 files present for conversion");
                            int maxDegreeOfParallelism = _appConfiguration.MaxDegreeOfParallelism;
                            int totalFileCount = 0;
                            DateTime increaseTwoMinute = DateTime.Now.AddMinutes(2);

                            while (totalFileCount <= hl7FilesList.Count)
                            {
                                if (increaseTwoMinute == DateTime.Now)
                                {
                                    maxDegreeOfParallelism += 10;
                                    increaseTwoMinute = DateTime.Now.AddMinutes(2);
                                }

                                _logger?.LogInformation($"Batch count with Skip: {totalFileCount} and Take: {maxDegreeOfParallelism}");
                                var fileList = hl7FilesList.Skip(totalFileCount).Take(maxDegreeOfParallelism).ToList();

                                _logger?.LogInformation($"IncreasedTime {increaseTwoMinute}");

                                if (isFileSkipped)
                                {
                                    UploadSkippedFile(fileList, ref hl7Response);
                                    hl7Response.IsFileSkipped = true;
                                }
                                else
                                {
                                    ParallelOptions parallelOptions = new();
                                    parallelOptions.MaxDegreeOfParallelism = maxDegreeOfParallelism;
                                    await Parallel.ForEachAsync(fileList, parallelOptions, async (Hl7File, CancellationToken) =>
                                    {
                                        if (Hl7File != null && Hl7File.HL7FileName != String.Empty && Hl7File.HL7FileType != String.Empty)
                                        {
                                            string hL7fileContent = string.Empty;
                                            
                                            BlobClient blobClient = blobContainer.GetBlobClient(Hl7File.HL7FileName);

                                            if (blobClient != null && await blobClient.ExistsAsync())
                                            {
                                                var blobData = await blobClient.OpenReadAsync();
                                                using (var streamReader = new StreamReader(blobData))
                                                {
                                                    hL7fileContent = await streamReader.ReadToEndAsync();
                                                }

                                                if (!string.IsNullOrEmpty(hL7fileContent))
                                                {

                                                    JObject jObject = Newtonsoft.Json.JsonConvert.DeserializeObject(hl7ConverterRequestTemplate) as JObject;

                                                    jObject["parameter"][0]["valueString"] = hL7fileContent;
                                                    jObject["parameter"][3]["valueString"] = Hl7File.HL7FileType;

                                                    string requestBody = Newtonsoft.Json.JsonConvert.SerializeObject(jObject, Newtonsoft.Json.Formatting.Indented);

                                                    try
                                                    {

                                                        _logger?.LogInformation($"Processing {Hl7File.HL7FileName} file to fhir server.");
                                                        httpResponseMessage = await _fhirClient.Send(requestBody, Hl7File.HL7FileName);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        _logger.LogInformation($"Error while sending Fhir request to Converter with exception:{ex.Message}");
                                                        _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));

                                                    }


                                                    var responseString = await httpResponseMessage.Content.ReadAsStringAsync() ?? "{}";
                                                    if (httpResponseMessage.IsSuccessStatusCode)
                                                    {
                                                        _logger?.LogInformation($"Fhir request completed successfully with status code {(int)httpResponseMessage.StatusCode} and {httpResponseMessage.ReasonPhrase} for file {Hl7File.HL7FileName}.");
                                                        await UploadToBlob(Hl7File.HL7FileName, _blobConfiguration.ValidatedContainer, _blobConfiguration.ConvertedContainer);

                                                        var validatedContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.Hl7ConverterJsonContainer);
                                                        var fhirBundleJson = new MemoryStream(Encoding.UTF8.GetBytes(responseString));
                                                        string fhirJsonFileName = Path.GetFileNameWithoutExtension(Hl7File.HL7FileName) + ".json";

                                                        if (!await validatedContainer.GetBlobClient(fhirJsonFileName).ExistsAsync())
                                                        {
                                                            await validatedContainer.UploadBlobAsync(fhirJsonFileName, fhirBundleJson);
                                                            fhirBundleJson.Close();
                                                        }
                                                    }
                                                    else
                                                    {
                                                        _logger?.LogInformation($"Error from Fhir server with status code {(int)httpResponseMessage.StatusCode} and {httpResponseMessage.ReasonPhrase} for file {Hl7File.HL7FileName}");
                                                        _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));

                                                        var isHttpRetryStatusCode = _appConfiguration.HttpFailStatusCodes.Split(',').Any(x => x.Trim() == ((int)httpResponseMessage.StatusCode).ToString());

                                                        _logger?.LogInformation($"failureCode check result  = {isHttpRetryStatusCode} for file {Hl7File.HL7FileName}");

                                                        await UploadToBlob(Hl7File.HL7FileName, _blobConfiguration.ValidatedContainer, _blobConfiguration.ConversionfailContainer);

                                                        isFileSkipped = !hL7ConverterInput.ProceedOnError ? true : false;
                                                        _logger?.LogInformation($"isFileSkipped {isFileSkipped}.");

                                                    }
                                                }
                                                else
                                                {
                                                    _logger?.LogInformation($"content not found for file {Hl7File.HL7FileName}.");

                                                }
                                            }
                                        }
                                    });
                                }
                                totalFileCount += maxDegreeOfParallelism;
                                _logger?.LogInformation($"TotalFileCount count : {totalFileCount}");
                            };
                            _logger?.LogInformation("Response : {Response}.", JsonConvert.SerializeObject(hl7Response));
                        }
                    }
                }

               

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id} Error while executing HL7Convereter Function App with exception", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));

            }

            return await Task.FromResult(JsonConvert.SerializeObject(hl7Response));
        }

        public async Task UploadToBlob(string fileName, string soruceBlobName, string targetBloblName)
        {
            var sourceClient = _blobServiceClient.GetBlobContainerClient(soruceBlobName);
            var targetClient = _blobServiceClient.GetBlobContainerClient(targetBloblName);
            BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            BlobClient targetBlobClient = targetClient.GetBlobClient(fileName);
            if (await sourceBlobClient.ExistsAsync() && !await targetBlobClient.ExistsAsync())
            {
                await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
                await sourceBlobClient.DeleteIfExistsAsync();
            }
        }

        /// <summary>
        /// Upload the remaning HL7 files to Skipped Container.
        /// </summary>
        /// <param name="fileList"></param>
        /// <param name="fhirResponse"></param>
        private void UploadSkippedFile(List<Hl7File> fileList, ref Hl7ConverterResponse hl7ConverterResponse)
        {
            foreach (var item in fileList)
            {
                _logger?.LogInformation($"UploadToSuccessBlob :Start uploading {item.HL7FileName} ");
                BlobContainerClient sourceClient = new(_blobConfiguration.BlobConnectionString, _blobConfiguration.ValidatedContainer);
                BlobContainerClient targetClient = new(_blobConfiguration.BlobConnectionString, _blobConfiguration.SkippedBlobContainer);
                BlobClient sourceBlobClient = sourceClient.GetBlobClient(item.HL7FileName);
                BlobClient targetBlobClient = targetClient.GetBlobClient(item.HL7FileName);

                try
                {
                    if (sourceBlobClient != null && sourceBlobClient.Exists())
                    {
                        var copy = targetBlobClient.StartCopyFromUri(sourceBlobClient.Uri);
                        copy.WaitForCompletion();
                        sourceBlobClient.DeleteIfExistsAsync();
                        _logger?.LogInformation($"UploadSkippedFile : Blob uploaded to {_blobConfiguration.SkippedBlobContainer} and deleted from {_blobConfiguration.ValidatedContainer} ");
                    }
                    else
                    {
                        _logger?.LogInformation($"UploadSkippedFile : Blob {item.HL7FileName} does not exists.");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogInformation($"UploadToSuccessBlob :Error while copying the {item.HL7FileName} from {_blobConfiguration.ValidatedContainer} to {_blobConfiguration.SkippedBlobContainer} ");
                    _logger?.LogError(ex, "{Name}-{Id} Error while copying the file from {soruceBlobName} to {targetBloblName}", Name, Id, _blobConfiguration.ValidatedContainer, _blobConfiguration.SkippedBlobContainer);
                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds));
                }

                Response response = new();
                response.FileName = item.HL7FileName;
                response.StatusCode = (int)HttpStatusCode.FailedDependency;
                response.Error = $"File skipped.";
                hl7ConverterResponse.response.Add(response);
            }
        }
    }
}
