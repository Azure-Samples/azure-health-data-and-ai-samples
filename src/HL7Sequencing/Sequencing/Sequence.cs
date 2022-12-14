using Azure.Storage.Blobs;
using HL7Sequencing.Configuration;
using HL7Sequencing.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NHapi.Base.Parser;
using NHapi.Model.V28.Segment;
using System.Net;
using System.Text;

namespace HL7Sequencing.Sequencing
{
    public class Sequence : ISequence
    {
        public Sequence(BlobConfig config, BlobServiceClient blobServiceClient, PipeParser pipeParser, TelemetryClient telemetryClient = null, ILogger<Sequence> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _config = config;
            _logger = logger;
            _telemetryClient = telemetryClient;
            _pipeParser = pipeParser;
            _blobServiceClient = blobServiceClient;
        }

        private readonly string _id;
        private readonly BlobConfig _config;
        private readonly PipeParser _pipeParser;
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly BlobServiceClient _blobServiceClient;

        public string Id => _id;
        public string Name => "Sequence";

        public int MaxParallelism => 230;

        public async Task<HttpResponseData> GetSequencListAsync(HttpRequestData request)
        {
            DateTime start = DateTime.Now;
            try
            {
                string reqContent = await new StreamReader(request.Body).ReadToEndAsync();

                if (!string.IsNullOrEmpty(reqContent))
                {
                    Object listLock = new Object();
                    List<Hl7Files> validatedHl7Files = new();
                    var postDataList = JsonConvert.DeserializeObject<List<PostData>>(reqContent);

                    if (postDataList != null && postDataList.Count > 0)
                    {
                        List<string> hl7files = postDataList.Select(x => x.HL7FileName).ToList();

                        var blobContainer = _blobServiceClient.GetBlobContainerClient(_config.ValidatedBlobContainer);

                        if (hl7files != null && hl7files.Count > 0)
                        {
                            ParallelOptions parallelOptions = new();
                            parallelOptions.MaxDegreeOfParallelism = MaxParallelism;

                            await Parallel.ForEachAsync(hl7files, parallelOptions, async (hl7FileName, CancellationToken) =>
                            {

                                if (hl7FileName != null && hl7FileName != String.Empty)
                                {
                                    BlobClient blobClient = blobContainer.GetBlobClient(hl7FileName);
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
                                                hl7Files.HL7FileName = hl7FileName;
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
                                                            var resynchronizationFileName = Path.GetFileNameWithoutExtension(hl7FileName) + "_" + DateTime.Now.ToString("MMddyyyyhhmmss") + Path.GetExtension(hl7FileName);
                                                            var hl7ReSynchronizationContainer = _blobServiceClient.GetBlobContainerClient(_config.Hl7ResynchronizationContainer);

                                                            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileData));
                                                            await hl7ReSynchronizationContainer.UploadBlobAsync(resynchronizationFileName, memoryStream);
                                                            memoryStream.Close();
                                                            await blobContainer.DeleteBlobAsync(hl7FileName);
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
                                                _logger?.LogError(ex, "{Name}-{Id} " + ex.Message, Name, Id);
                                                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                                                throw;
                                            }
                                        }
                                    }
                                }
                            });
                        }
                    }

                    if (validatedHl7Files.Any())
                    {

                        var hl7SequnceList = validatedHl7Files.OrderBy(hl7Files => hl7Files.DateTimeOffsetOfMessage)
                                                              .ThenBy(hl7Files => String.IsNullOrEmpty(hl7Files.SequenceNumber))
                                                              .ThenBy(hl7Files => hl7Files.SequenceNumber)
                                                              .ThenBy(hl7Files => hl7Files.HL7FileName);

                        if (hl7SequnceList != null && hl7SequnceList.Count() > 0)
                        {
                            var SequenceList = hl7SequnceList.Select(x => new { x?.HL7FileName, x?.HL7FileType });
                            var jsonResponseToReturn = JsonConvert.SerializeObject(SequenceList);
                            var response = request.CreateResponse(HttpStatusCode.OK);
                            response.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonResponseToReturn));
                            return await Task.FromResult(response);
                        }
                    }


                    var jsonEmptyResponseToReturn = JsonConvert.SerializeObject(new List<string>());
                    var emptyBlobresponse = request.CreateResponse(HttpStatusCode.OK);
                    emptyBlobresponse.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonEmptyResponseToReturn));
                    return await Task.FromResult(emptyBlobresponse);
                }

                var noContentresponse = request.CreateResponse(HttpStatusCode.NoContent);
                return await Task.FromResult(noContentresponse);

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id} validation message.", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Body = new MemoryStream(Encoding.UTF8.GetBytes(ex.Message));
                return await Task.FromResult(errorResponse);
            }

        }
    }
}
