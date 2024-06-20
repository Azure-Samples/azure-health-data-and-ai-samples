using Blazored.Modal.Services;
using FhirBlaze.Model;
using FhirBlaze.Shared;
using FhirBlaze.SharedComponents.Services;
using Hl7.Fhir.Model;
using Hl7.Fhir.Support;
using Hl7.Fhir.Utility;
using Microsoft.AspNetCore.Components;
using Microsoft.Graph;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace FhirBlaze.Pages
{
    public partial class BatchTranslate
    {

        [Inject]
        IAPIMService apmService { get; set; }

        [Parameter]
        public TSFhirModel? tsFhirModel { get; set; }

        [Parameter]
        public BatchTranslateModel? tsBatchTraslateModel { get; set; }
        [Parameter]
        public CodingEntry tsCodingEntry { get; set; }

        [Inject]
        IJSRuntime runtime { get; set; }

        [CascadingParameter] public IModalService Modal { get; set; } = default!;

        protected override async void OnInitialized()
        {
            base.OnInitialized();
            if (tsFhirModel == null)
            {
                tsFhirModel = new TSFhirModel();
                tsBatchTraslateModel = new BatchTranslateModel();
            }

        }

        public async void GetBatchDetails(string codeSystem)
        {
            try
            {
                tsFhirModel.observationJson = string.Empty;
                tsFhirModel.LookUpAndTranslateJson = string.Empty;
                tsFhirModel.batchJson = string.Empty;
                tsBatchTraslateModel = new BatchTranslateModel(); 
              // fetch data from json and display to ui 
              var observations = await Http.GetStringAsync("data/BatchTranslateData.json");
                JObject bundleObject = JObject.Parse(observations);

                JObject codeObject = bundleObject.GetValue(codeSystem) as JObject;
                if (codeObject != null)
                {
                    if (codeObject.ContainsKey("entry"))
                    {
                        JArray entryArray = (JArray)codeObject["entry"];
                        tsBatchTraslateModel= GetConvertedRequest(entryArray);
                         JObject convertedObj = JObject.FromObject(tsBatchTraslateModel);
                        tsFhirModel.observationJson = codeObject.ToString();
                        tsFhirModel.batchJson = convertedObj.ToString();
                        StateHasChanged();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception");
                Console.WriteLine(e.Message); //manage the cancel search
            }
        }

        public async void BatchTranslateMethod()
        {
            JArray array = new JArray();
            try
            {
                // Temp comment for offline testing 

                JObject jtranslatedCode = new JObject();
                //JObject bundleObject = JObject.Parse(tsFhirModel.observationJson);
                //var translatedCode = await apmService.BatchTranslateCode(bundleObject.ToString());
                //if (translatedCode.IsSuccessStatusCode)
                //{
                //  var translateJsonResponse = translatedCode.Content.ReadAsStringAsync().Result;
                var translateJsonResponse = await Http.GetStringAsync("data/tempResponse.json");
                JObject translatedJobject = JObject.Parse(translateJsonResponse);
                if (translatedJobject.ContainsKey("entry"))
                {
                    JArray entryArrayResponse = (JArray)translatedJobject["entry"];
                    tsBatchTraslateModel = new BatchTranslateModel();
                    tsBatchTraslateModel = GetConvertedResponse(entryArrayResponse);
                    JObject convertedObj = JObject.FromObject(tsBatchTraslateModel);    
                    tsFhirModel.LookUpAndTranslateJson = convertedObj.ToString();
                    StateHasChanged();
                }
                //}
                //else
                //{
                //    Console.WriteLine("Error Code :" + translatedCode.StatusCode.ToString());
                //    Console.WriteLine(translatedCode.Content.ReadAsStringAsync().Result);
                //    tsFhirModel.observationJson = "Received error response.";
                //    tsFhirModel.LookUpAndTranslateJson = string.Empty;
                //    StateHasChanged();
                //}
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception");
                Console.WriteLine(e.Message); //manage the cancel search
            }
        }
         
        private BatchTranslateModel GetConvertedRequest(JArray entryArray)
        {
            try {
                foreach (var entry in entryArray)
                {
                    tsCodingEntry = new CodingEntry();
                    JObject resourceObject = (JObject)entry["resource"];
                    string test = resourceObject.ToString();

                    tsCodingEntry.code = (string)resourceObject["parameter"]
                    .FirstOrDefault(p => (string)p["name"] == "code")?["valueString"];

                    tsCodingEntry.system = (string)resourceObject["parameter"]
                        .FirstOrDefault(p => (string)p["name"] == "system")?["valueUri"];

                    tsCodingEntry.targetsystem = (string)resourceObject["parameter"]
                        .FirstOrDefault(p => (string)p["name"] == "targetsystem")?["valueUri"];
                    tsCodingEntry.display = string.Empty;
                    tsBatchTraslateModel.Coding.Add(tsCodingEntry);
                }
                return tsBatchTraslateModel;
            }
            catch (Exception ex)
            {
                return tsBatchTraslateModel;
            }
        }

        private BatchTranslateModel GetConvertedResponse(JArray entryArray)
        {
            try
            {
                foreach (var entry in entryArray)
                {
                    tsCodingEntry = new CodingEntry();
                    JObject resourceObject = (JObject)entry["response"];
                    string test = resourceObject.ToString();

                    tsCodingEntry.code = (string)resourceObject["outcome"]["parameter"]
                            .FirstOrDefault(p => (string)p["name"] == "match")
                            ?["part"]?[0]?["valueCoding"]?["code"];

                    tsCodingEntry.system = (string)resourceObject["outcome"]["parameter"]
                       .FirstOrDefault(p => (string)p["name"] == "match")
                       ?["part"]?[0]?["valueCoding"]?["system"];
                    tsCodingEntry.display = (string)resourceObject["outcome"]["parameter"]
                       .FirstOrDefault(p => (string)p["name"] == "match")
                       ?["part"]?[0]?["valueCoding"]?["display"];
                    tsCodingEntry.targetsystem = string.Empty;
                    tsBatchTraslateModel.Coding.Add(tsCodingEntry);
                }
                return tsBatchTraslateModel;
            }
            catch (Exception ex)
            { 
                return tsBatchTraslateModel;
            }
        }
    }
}
