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
        public Sequence(BlobConfig config, TelemetryClient telemetryClient = null, ILogger<Sequence> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _config = config;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        private readonly string _id;
        private readonly BlobConfig _config;
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;

        public string Id => _id;
        public string Name => "Sequence";

        public async Task<HttpResponseData> GetSequencListAsync(HttpRequestData request)
        {
            DateTime start = DateTime.Now;
            try
            {
                string reqContent = await new StreamReader(request.Body).ReadToEndAsync();

                if (!string.IsNullOrEmpty(reqContent))
                {
                    List<Hl7Files> validatedHl7Files = new();
                    var postDataList = JsonConvert.DeserializeObject<List<PostData>>(reqContent);

                    if (postDataList != null && postDataList.Count > 0)
                    {
                        List<string> hl7files = postDataList.Select(x => x.HL7FileName).ToList();

                        BlobContainerClient blobContainer = new(_config.BlobConnectionString, _config.ValidatedBlobContainer);

                        if (hl7files != null && hl7files.Count > 0)
                        {
                            foreach (string hl7FileName in hl7files)
                            {
                                if (hl7FileName != null)
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
                                                var parser = new PipeParser();
                                                var parsedMessage = parser.Parse(fileData);
                                                if (parsedMessage != null)
                                                {
                                                    hl7Files.HL7FileType = parsedMessage?.GetType().Name;
                                                    var MSH = parsedMessage?.GetStructure("MSH") as MSH;

                                                    var MSH13 = MSH?.SequenceNumber.Value;

                                                    if (MSH13 != null && MSH13 != "")
                                                    {
                                                        if (MSH13 == "0" || MSH13 == "-1")
                                                        {

                                                            var resynchronizationFileName = Path.GetFileNameWithoutExtension(hl7FileName) + "_" + DateTime.Now.ToString("MMddyyyyhhmmss") + Path.GetExtension(hl7FileName);
                                                            BlobContainerClient hl7ReSynchronizationContainer = new BlobContainerClient(_config.BlobConnectionString, _config.Hl7ResynchronizationContainer);

                                                            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileData));
                                                            await hl7ReSynchronizationContainer.UploadBlobAsync(resynchronizationFileName, memoryStream);
                                                            memoryStream.Close();
                                                            await blobContainer.DeleteBlobAsync(hl7FileName);
                                                            continue;
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
                                                            TimeSpan timeSpan = new TimeSpan(gmtOffset / 100, gmtOffset % 100, 0);
                                                            hl7Files.DateTimeOffsetOfMessage = new DateTimeOffset(MSH7.Year, MSH7.Month, MSH7.Day, MSH7.Hour, MSH7.Minute, MSH7.Second, Convert.ToInt32(MSH7.FractSecond), timeSpan);
                                                        }
                                                    }

                                                    validatedHl7Files.Add(hl7Files);

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
                            }
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
