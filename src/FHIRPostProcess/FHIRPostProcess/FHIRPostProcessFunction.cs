using Azure.Storage.Blobs;
using FHIRPostProcess.Configuration;
using FHIRPostProcess.Model;
using FHIRPostProcess.PostProcessor;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace FHIRPostProcess
{
    public class FHIRPostProcessFunction
    {
        private readonly IPostProcess _postProcess;
        private AppConfiguration _appConfiguration;
        private readonly ILogger _logger;
        private readonly BlobConfiguration _blobConfiguration;

        public FHIRPostProcessFunction(IPostProcess postProcess, AppConfiguration appConfiguration, BlobConfiguration blobConfiguration, ILogger<FHIRPostProcessFunction> logger)
        {
            _postProcess = postProcess;
            _appConfiguration = appConfiguration;
            _logger = logger;
            _blobConfiguration = blobConfiguration;
        }

        [Function(nameof(PostProcessOrchestration))]
        public async Task<string> PostProcessOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            try
            {
                int totalFileCount = 0;
                var parallelTasks = new List<Task<string>>();
                List<Hl7File> hl7FilesList = new();
                string input_context = context.GetInput<string>();
                FHIRPostProcessInput fHIRPostProcessInput = new();
                fHIRPostProcessInput = JsonConvert.DeserializeObject<FHIRPostProcessInput>(input_context);
                string hl7FilesArray = string.Empty;
                var hl7ArrayFileName = System.Text.Encoding.Default.GetString(Convert.FromBase64String(fHIRPostProcessInput.Hl7ArrayFileName));

                _logger?.LogInformation($"PostProcessOrchestration Task execution start:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                if (fHIRPostProcessInput != null && !string.IsNullOrEmpty(hl7ArrayFileName))
                {
                    
                    _logger?.LogInformation($"container name:"+_blobConfiguration.ValidatedContainer);

                    hl7FilesArray = await context.CallActivityAsync<string>(nameof(GetHl7FilesListActivityTrigger), hl7ArrayFileName);

                    if (!string.IsNullOrEmpty(hl7FilesArray))
                    {
                        hl7FilesList = JsonConvert.DeserializeObject<List<Hl7File>>(hl7FilesArray);
                    }

                    if (hl7FilesList != null && hl7FilesList.Count > 0)
                    {
                        
                        while (totalFileCount <= hl7FilesList.Count)
                        {
                            var files = hl7FilesList.Skip(totalFileCount).Take(_appConfiguration.FHIRPostProcessMaxParallelism).ToList();
                            OrchestrationInput orchestrationInput = new()
                            {
                                Hl7Files = files,
                                FhirBundleType = fHIRPostProcessInput.FhirBundleType
                            };
                            Task<string> result = context.CallActivityAsync<string>(nameof(PostProcessActivityTrigger), orchestrationInput);
                            parallelTasks.Add(result);
                            totalFileCount += _appConfiguration.FHIRPostProcessMaxParallelism;
                        }

                    }
                }

                _logger?.LogInformation($"PostProcessOrchestration Task execution end:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                await Task.WhenAll(parallelTasks);
                string parallelResult = string.Empty;
                foreach (var item in parallelTasks)
                {
                    if (!string.IsNullOrEmpty(item.Result))
                    {
                        parallelResult += item.Result;
                    }
                }
                return parallelResult.Trim();
            }
            catch(Exception ex)
            {
                _logger?.LogInformation($"Exception at PostProcessOrchestration:" + ex.Message);
                throw;
            }
            
        }


        [Function(nameof(PostProcessActivityTrigger))]
        public async Task<string> PostProcessActivityTrigger([ActivityTrigger] OrchestrationInput orchestrationInput, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(PostProcessActivityTrigger));
            return await _postProcess.PostProcessResources(orchestrationInput);
        }

        [Function(nameof(GetHl7FilesListActivityTrigger))]
        public async Task<string> GetHl7FilesListActivityTrigger([ActivityTrigger] string hl7ArrayFileName, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(GetHl7FilesListActivityTrigger));
            return await _postProcess.GetHl7FilesList(hl7ArrayFileName);
        }

        [Function(nameof(FHIRPostProcess_HTTP))]
        public static async Task<HttpResponseData> FHIRPostProcess_HTTP(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(FHIRPostProcess_HTTP));
            string body = new StreamReader(req.Body).ReadToEnd();
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(PostProcessOrchestration), body);
            logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);
            return client.CreateCheckStatusResponse(req, instanceId);
        }

    }
}
