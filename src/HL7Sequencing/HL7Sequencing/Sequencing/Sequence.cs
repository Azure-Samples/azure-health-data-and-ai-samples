using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HL7Sequencing.Configuration;
using HL7Sequencing.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NHapi.Base.Parser;
using NHapi.Model.V28.Segment;
using System.Configuration;
using System.Net;
using System.Reflection.Metadata;
using System.Text;

namespace HL7Sequencing.Sequencing
{
    public class Sequence : ISequence
    {
        public Sequence(BlobConfig config, AppConfiguration appConfiguration, BlobServiceClient blobServiceClient, PipeParser pipeParser, TelemetryClient telemetryClient = null, ILogger<Sequence> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _config = config;
            _logger = logger;
            _telemetryClient = telemetryClient;
            _pipeParser = pipeParser;
            _blobServiceClient = blobServiceClient;
            _appConfiguration = appConfiguration;
        }

        private readonly string _id;
        private readonly BlobConfig _config;
        private readonly PipeParser _pipeParser;
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly AppConfiguration _appConfiguration;

        public string Id => _id;
        public string Name => "Sequence";


        public async Task<string> GetSequencListAsync(string request)
        {
            DateTime start = DateTime.Now;
            List<Hl7File> hl7Files = new();
            int maxDegreeOfParallelism;
            bool proceedOnError = true;
            try
            {
                if (!string.IsNullOrEmpty(request))
                {
                    _logger?.LogInformation($"HL7Sequencing method start at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                    Object listLock = new Object();
                    List<Hl7Files> validatedHl7Files = new();
                    var postData = JsonConvert.DeserializeObject<PostData>(request);
                    string hl7FilesArray = string.Empty;
                    var hl7ArrayFileName = System.Text.Encoding.Default.GetString(Convert.FromBase64String(postData.Hl7ArrayFileName));

                    if (postData != null && !string.IsNullOrEmpty(hl7ArrayFileName))
                    {
                        var blobContainer = _blobServiceClient.GetBlobContainerClient(_config.ValidatedBlobContainer);
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
                                //_logger?.LogInformation($"Hl7Files count: {hl7FilesList.Count}");

                                maxDegreeOfParallelism = _appConfiguration.HL7SequencingMaxParallelism;

                                ParallelOptions parallelOptions = new();
                                parallelOptions.MaxDegreeOfParallelism = maxDegreeOfParallelism;
                                hl7Files = hl7FilesList;

                                await Parallel.ForEachAsync(hl7Files, parallelOptions, async (hl7File, CancellationToken) =>
                                {
                                    if (hl7File != null && !string.IsNullOrEmpty(hl7File.HL7FileName))
                                    {
                                        BlobClient blobClient = blobContainer.GetBlobClient(hl7File.HL7FileName);
                                        if (blobClient != null && await blobClient.ExistsAsync())
                                        {
                                            string fileData = string.Empty;
                                            Hl7Files hl7Files = new();
                                            var blobData = await blobClient.OpenReadAsync();
                                            using (var streamReader = new StreamReader(blobData))
                                            {
                                                fileData = await streamReader.ReadToEndAsync();
                                            }

                                            //Use NHapi to read HL7 messages                            
                                            if (fileData != string.Empty && fileData != null)
                                            {
                                                try
                                                {
                                                    hl7Files.HL7FileName = hl7File.HL7FileName;
                                                    var parsedMessage = _pipeParser.Parse(fileData);
                                                    if (parsedMessage != null)
                                                    {
                                                        var MSH = parsedMessage?.GetStructure("MSH") as MSH;
                                                        hl7Files.HL7FileType = MSH?.MessageType.MessageStructure.Value;
                                                        var MSH13 = MSH?.SequenceNumber.Value;

                                                        if (MSH13 != null && MSH13 != "")
                                                        {
                                                            if (MSH13 == "0" || MSH13 == "-1")
                                                            {
                                                                var resynchronizationFileName = Path.GetFileNameWithoutExtension(hl7File.HL7FileName) + "_" + DateTime.Now.ToString("MMddyyyyhhmmss") + Path.GetExtension(hl7File.HL7FileName);
                                                                var hl7ReSynchronizationContainer = _blobServiceClient.GetBlobContainerClient(_config.Hl7ResynchronizationContainer);

                                                                var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileData));
                                                                await hl7ReSynchronizationContainer.UploadBlobAsync(resynchronizationFileName, memoryStream);
                                                                memoryStream.Close();
                                                                await blobContainer.DeleteBlobAsync(hl7File.HL7FileName);
                                                                return;
                                                            }
                                                            else
                                                            {
                                                                hl7Files.SequenceNumber = MSH13;
                                                            }
                                                        }

                                                        var MSH7 = MSH?.DateTimeOfMessage;
                                                        if (MSH7 != null)
                                                        {
                                                            if (MSH7.GMTOffset == -99)
                                                            {

                                                                DateTimeOffset dateTimeWithoutOffsetValue = new DateTimeOffset(MSH7.Year, MSH7.Month, MSH7.Day, MSH7.Hour, MSH7.Minute, MSH7.Second, Convert.ToInt32(MSH7.FractSecond), DateTimeOffset.UtcNow.Offset);
                                                                hl7Files.DateTimeOffsetOfMessage = dateTimeWithoutOffsetValue;
                                                            }
                                                            else
                                                            {
                                                                var gmtOffset = MSH7.GMTOffset;
                                                                TimeSpan timeSpan = new(gmtOffset / 100, gmtOffset % 100, 0);
                                                                hl7Files.DateTimeOffsetOfMessage = new DateTimeOffset(MSH7.Year, MSH7.Month, MSH7.Day, MSH7.Hour, MSH7.Minute, MSH7.Second, Convert.ToInt32(MSH7.FractSecond), timeSpan);
                                                            }
                                                        }

                                                        lock (listLock)
                                                        {
                                                            validatedHl7Files.Add(hl7Files);
                                                        }

                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    var exMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                                                    _logger?.LogError(ex, "{Name}-{Id} " + exMessage, Name, Id);
                                                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                                                    if (postData.ProceedOnError == false)
                                                    {
                                                        proceedOnError = false;
                                                        throw new Exception("FileName: " + hl7File.HL7FileName + ",Error Message: " + exMessage);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                });
                            }
                        }

                    }

                    if (validatedHl7Files.Any())
                    {
                        _logger?.LogInformation($"Valid Hl7File count for HL7Sequencing:" + validatedHl7Files.Count);

                        var hl7SequnceList = validatedHl7Files.OrderBy(hl7Files => hl7Files.DateTimeOffsetOfMessage)
                                                              .ThenBy(hl7Files => String.IsNullOrEmpty(hl7Files.SequenceNumber))
                                                              .ThenBy(hl7Files => hl7Files.SequenceNumber)
                                                              .ThenBy(hl7Files => hl7Files.HL7FileName);

                        if (hl7SequnceList != null && hl7SequnceList.Count() > 0)
                        {
                            var SequenceList = hl7SequnceList.Select(x => new { x?.HL7FileName, x?.HL7FileType });
                            var SequenceListJson = JsonConvert.SerializeObject(SequenceList);

                            var validatedContainer = _blobServiceClient.GetBlobContainerClient(_config.ValidatedBlobContainer);
                            BlobClient blobClient = validatedContainer.GetBlobClient(hl7ArrayFileName);

                            if (await blobClient.ExistsAsync())
                            {
                                var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(SequenceListJson));
                                await blobClient.UploadAsync(memoryStream, overwrite: true);
                                //await validatedContainer.UploadBlobAsync(Hl7FilesArrayFileName, memoryStream);
                                memoryStream.Close();
                            }

                            _logger?.LogInformation($"HL7Sequencing method end at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                            return await Task.FromResult(hl7ArrayFileName);
                        }
                    }

                    _logger?.LogInformation($"HL7Sequencing completed at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                    
                    return await Task.FromResult(string.Empty);
                }

                return await Task.FromResult(string.Empty);

            }
            catch (Exception ex)
            {
                _logger?.LogInformation($"HL7Sequencing with exception: {ex.Message}");
                _logger?.LogError(ex, "{Name}-{Id} validation message.", Name, Id);

                if (!proceedOnError && hl7Files.Any() && !string.IsNullOrEmpty(_config.ValidatedBlobContainer))
                {
                    foreach (var blob in hl7Files)
                    {
                        if (!string.IsNullOrEmpty(blob.HL7FileName))
                        {
                            await UploadToBlob(blob.HL7FileName, _config.ValidatedBlobContainer, _config.Hl7skippedContainer);
                        }
                    }
                }

                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                throw;
            }

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
                await sourceBlobClient.DeleteAsync();
            }

        }
    }
}
