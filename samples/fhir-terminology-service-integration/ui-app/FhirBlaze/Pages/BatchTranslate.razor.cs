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

        [Parameter]
        public BatchValidateModel? tsBatchValidateModel { get; set; }
        [Parameter]
        public CodingValidateEntry tsCodingValidateEntry { get; set; }
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
                tsBatchValidateModel = new BatchValidateModel();
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
                tsBatchValidateModel = new BatchValidateModel();
                // fetch data from json and display to ui 
                var observations = await Http.GetStringAsync("data/BatchObservation.json");
                JObject bundleObject = JObject.Parse(observations);

                JObject codeObject = bundleObject.GetValue(codeSystem) as JObject;
                if (codeObject != null)
                {
                    if (codeObject.ContainsKey("entry"))
                    {
                        JObject convertedObj;
                        JArray entryArray = (JArray)codeObject["entry"];
                        string urlValue = (string)entryArray[0]["request"]["url"];
                        if (urlValue.Contains("validate"))
                        {
                            tsBatchValidateModel = GetConvertedValidateRequest(entryArray);
                            convertedObj = JObject.FromObject(tsBatchValidateModel);
                            convertedObj = RemoveNullParameters(convertedObj);
                        }
                        else
                        {
                            tsBatchTraslateModel = GetConvertedTranslatedRequest(entryArray);
                            convertedObj = JObject.FromObject(tsBatchTraslateModel);
                        }

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
                //  var translateJsonResponse = await Http.GetStringAsync("data/tempResponse.json");
                var translateJsonResponse = await Http.GetStringAsync("data/tempValidateResponse.json");
                JObject translatedJobject = JObject.Parse(translateJsonResponse);
                if (translatedJobject.ContainsKey("entry"))
                {
                    JObject convertedObj = null;
                    JToken urlParameter = translatedJobject.SelectToken("$.parameter[?(@.name == 'match')]");
                    if (urlParameter != null)
                    {
                        JArray entryArrayResponse = (JArray)translatedJobject["entry"];
                        tsBatchTraslateModel = new BatchTranslateModel();
                        tsBatchTraslateModel = GetConvertedResponse(entryArrayResponse);
                        convertedObj = JObject.FromObject(tsBatchTraslateModel);
                    }
                    else
                    {
                        JArray entryArrayResponse = (JArray)translatedJobject["entry"];
                        tsBatchValidateModel = new BatchValidateModel();
                        tsBatchValidateModel = GetConvertedValidateResponse(entryArrayResponse);
                        convertedObj = JObject.FromObject(tsBatchValidateModel);
                        convertedObj = RemoveNullParameters(convertedObj); // to remove empty parameter



                    }

                    string test5=convertedObj.ToString();
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

        private BatchTranslateModel GetConvertedTranslatedRequest(JArray entryArray)
        {
            try
            {
                // check for req - translate or validate
                string test1 = entryArray.ToString();
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

        private BatchValidateModel GetConvertedValidateRequest(JArray entryArray)
        {
            try
            {
                // check for req - translate or validate
                string test1 = entryArray.ToString();
                foreach (var entry in entryArray)
                {
                    //string test2 = entry.ToString();
                    tsCodingValidateEntry = new CodingValidateEntry();
                    JObject resourceObject = (JObject)entry["resource"];
                    string test = resourceObject.ToString();

                    tsCodingValidateEntry.code = (string)resourceObject["parameter"]
                    .FirstOrDefault(p => (string)p["name"] == "code")?["valueString"];

                    JToken urlParameter = resourceObject.SelectToken("$.parameter[?(@.name == 'url')]");
                    if (urlParameter != null)
                    {
                        tsCodingValidateEntry.url = (resourceObject["parameter"]
                        .FirstOrDefault(p => (string)p["name"] == "url")?["valueUri"]?.ToString());
                    }

                    JToken urlDate = resourceObject.SelectToken("$.parameter[?(@.name == 'date')]");
                    if (urlDate != null)
                    {
                        tsCodingValidateEntry.date = (resourceObject["parameter"]
                        .FirstOrDefault(p => (string)p["name"] == "date")?["valueDateTime"]?.ToString());
                    }
                    JToken urlSystem = resourceObject.SelectToken("$.parameter[?(@.name == 'system')]");
                    if (urlSystem != null)
                    {
                        tsCodingValidateEntry.system = (resourceObject["parameter"]
                        .FirstOrDefault(p => (string)p["name"] == "system")?["valueString"]?.ToString());
                    }
                    JToken urlValueSetVersion = resourceObject.SelectToken("$.parameter[?(@.name == 'valueSetVersion')]");
                    if (urlValueSetVersion != null)
                    {
                        tsCodingValidateEntry.valueSetVersion = (resourceObject["parameter"]
                        .FirstOrDefault(p => (string)p["name"] == "valueSetVersion")?["valueString"]?.ToString());
                    }
                    tsBatchValidateModel.Coding.Add(tsCodingValidateEntry);
                }
                return tsBatchValidateModel;
            }
            catch (Exception ex)
            {
                return tsBatchValidateModel;
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


        private BatchValidateModel GetConvertedValidateResponse(JArray entryArray)
        {
            try
            {
                foreach (var entry in entryArray)
                {
                    tsCodingValidateEntry = new CodingValidateEntry();
                    JObject resourceObject = (JObject)entry["response"];
                    string test = resourceObject.ToString();

                    tsCodingValidateEntry.result = (bool)resourceObject["outcome"]["parameter"]
                            .FirstOrDefault(p => (string)p["name"] == "result")
                            ?["valueBoolean"];

                    JToken urlresult = resourceObject.SelectToken("$.outcome.parameter[?(@.name == 'message')]");
                    if (urlresult != null)  
                    {
                        tsCodingValidateEntry.message = (string)resourceObject["outcome"]["parameter"]
                                .FirstOrDefault(p => (string)p["name"] == "message")
                                ?["valueString"];
                    }

                    JToken urldisplay = resourceObject.SelectToken("$.outcome.parameter[?(@.name == 'display')]");
                    if (urldisplay != null)
                    {
                        tsCodingValidateEntry.display = (string)resourceObject["outcome"]["parameter"]
                            .FirstOrDefault(p => (string)p["name"] == "display")
                            ?["valueString"];
                    }
                    tsBatchValidateModel.Coding.Add(tsCodingValidateEntry);
                }
                return tsBatchValidateModel;
            }
            catch (Exception ex)
            {
                return tsBatchValidateModel;
            }
        }

        private JObject RemoveNullParameters(JObject obj)
        {
            JArray codingArray = (JArray)obj["Coding"];
             
            foreach (JObject codingObject in codingArray.Children<JObject>().ToList())
            { 
                var propertiesToRemove = codingObject.Properties()
                    .Where(p => p.Value.Type == JTokenType.Null)
                    .Select(p => p.Name)
                    .ToList();

                foreach (var propertyName in propertiesToRemove)
                {
                    codingObject.Remove(propertyName);
                }
            }
            return obj;
        }
    }
}
