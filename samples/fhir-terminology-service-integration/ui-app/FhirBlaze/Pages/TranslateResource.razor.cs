using Azure;
using Blazored.Modal.Services;
using FhirBlaze.Model;
using FhirBlaze.Shared;
using FhirBlaze.SharedComponents.Services;
using Hl7.Fhir.Support;
using Hl7.Fhir.Utility;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FhirBlaze.Pages
{
    public partial class TranslateResource
    {
        
        [Inject]
        IAPIMService apmService { get; set; }

        [Parameter]
        public TSFhirModel? tsFhirModel { get; set; }

        [Inject]
        IJSRuntime runtime { get; set; }

        [CascadingParameter] public IModalService Modal { get; set; } = default!;

        protected override void OnInitialized()
        {

            base.OnInitialized();
            if (tsFhirModel == null)
            {
                tsFhirModel = new TSFhirModel();
            }

        }

        public async void SearchPatientObservations()
        {
            try
            {
                Console.WriteLine("Calling Search Patient Observation");
                var patientObservations = await apmService.GetPatientObservations(tsFhirModel.FirstName, tsFhirModel.LastName);
                if (patientObservations.IsSuccessStatusCode)
                {

                    Console.WriteLine(patientObservations.Content.ReadAsStringAsync().Result);
                    var bundleJson = patientObservations.Content.ReadAsStringAsync().Result;
                    JObject bundleObject = JObject.Parse(bundleJson);

                    if (bundleObject.ContainsKey("entry"))
                    {
                        var entryArray = bundleObject.GetValue("entry");
                        JArray sortedEntryArray = new JArray(entryArray.OrderByDescending(o => (DateTime)o["resource"]["issued"]));
                        JObject latestObservationEntry = sortedEntryArray.FirstOrDefault() as JObject;
                        if (latestObservationEntry.ContainsKey("resource"))
                        {
                            JObject observationResource = latestObservationEntry["resource"] as JObject;
                            tsFhirModel.observationJson = observationResource.ToString();
                            tsFhirModel.LookUpAndTranslateJson = string.Empty;
                            StateHasChanged();
                        }
                    }
                    else
                    {
                        tsFhirModel.observationJson = "No observation found.";
                        tsFhirModel.LookUpAndTranslateJson = string.Empty;
                        StateHasChanged();
                    }
                }
                else
                {
                    Console.WriteLine("Error Code :" + patientObservations.StatusCode.ToString());
                    Console.WriteLine(patientObservations.Content.ReadAsStringAsync().Result);
                    tsFhirModel.observationJson = "Received error response.";
                    tsFhirModel.LookUpAndTranslateJson = string.Empty;
                    StateHasChanged();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception");
                Console.WriteLine(e.Message); //manage the cancel search
            }
        }

        public async void Translate()
        {
            var addToIndex = 0;
            JArray array = new JArray();
            try
            {
                JObject bundleObject = JObject.Parse(tsFhirModel.observationJson);
                var paths = bundleObject
                                .Descendants()
                                .Where(x => x is JObject)
                                .Where(x => x["system"] != null && x["code"] != null && x["display"] != null)
                                .Select(x => x.Path)
                                .ToList();
                foreach (var path in paths)
                {
                    var sourceCode = bundleObject.SelectToken(path);
                    var system = (string)sourceCode["system"];
                    if (Convert.ToString(sourceCode["system"]).Contains("snomed"))
                    {
                        var translatedCode = await TranslateCode(sourceCode);
                        JObject translatedObject = new JObject(new JProperty("path", path), new JProperty("translated", translatedCode));
                        array.Add(translatedObject);
                    }
                    if (Convert.ToString(sourceCode["system"]).Contains("local-codes"))
                    {
                        var translatedCode = await TranslateCode(sourceCode);
                        JObject translatedObject = new JObject(new JProperty("path", path), new JProperty("translated", translatedCode));
                        array.Add(translatedObject);
                    }
                }
                var reversedArray = array.Reverse();
                foreach (var item in reversedArray)
                {
                    AddToObject((string)item["path"], bundleObject, (JObject)item["translated"]);
                }

                //foreach (var item in array)
                //{
                //    if (addToIndex <= 0)
                //    {
                //        AddToObject((string)item["path"], bundleObject, (JObject)item["translated"]);
                //        addToIndex++;
                //    }
                //    else
                //    {
                //        string path = (string)item["path"];
                //        StringBuilder sbPath = new StringBuilder(path);
                //        var indexOfItemNumber = path.LastIndexOf(']') - 1;
                //        char itemNumber = (char)path.ElementAt(indexOfItemNumber);
                //        int itemNumberNew = itemNumber + addToIndex;
                //        sbPath[indexOfItemNumber] = (char)itemNumberNew;
                //        path = sbPath.ToString();
                //        AddToObject(path, bundleObject, (JObject)item["translated"]);
                //        addToIndex++;
                //    }
                //}

                //var codeTokens = GetAllCodes(bundleObject, "coding");
                //JObject jArray = JObject.Parse(await TranslateObservationCodes(codeTokens));
                //string str = "";
                //foreach (var codeToken in jArray)
                //{
                //    str += codeToken.ToString();
                //}
                //Console.WriteLine(str);
                tsFhirModel.LookUpAndTranslateJson = bundleObject.ToString();
                StateHasChanged();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception");
                Console.WriteLine(e.Message); //manage the cancel search
            }
        }

        public async void SaveObservation()
        {
            try
            {
                JObject bundleObject = JObject.Parse(tsFhirModel.LookUpAndTranslateJson);
                var obvId = (string)bundleObject["id"];
                var SaveObservationResponse = await apmService.SaveObservation(obvId, bundleObject.ToString(Formatting.None));
                if (SaveObservationResponse.IsSuccessStatusCode)
                {
                    var SaveObservationJsonResponse = SaveObservationResponse.Content.ReadAsStringAsync().Result;
                    Object jObject = JObject.Parse(SaveObservationJsonResponse);

                    tsFhirModel.observationJson = tsFhirModel.LookUpAndTranslateJson;
                    tsFhirModel.LookUpAndTranslateJson = jObject.ToString();
                    StateHasChanged();
                }
                else
                {
                    tsFhirModel.observationJson = "Saving resource failed!";
                    tsFhirModel.LookUpAndTranslateJson = string.Empty;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                tsFhirModel.observationJson = ex.Message;
                tsFhirModel.LookUpAndTranslateJson = string.Empty;
                StateHasChanged();
            }
        }

        public async void ResetObservations()
        {
            try
            {
                var observations = await Http.GetStringAsync("data/Observations.json");
                var translateResponse = await apmService.ResetObservations(observations);
                if (translateResponse.IsSuccessStatusCode)
                {
                    var translateJsonResponse = translateResponse.Content.ReadAsStringAsync().Result;
                    await runtime.InvokeVoidAsync("alert", "Reset done!").ConfigureAwait(false);
                }
                else
                {
                    await runtime.InvokeVoidAsync("alert", "Reset failed!").ConfigureAwait(false);
                }

                tsFhirModel.observationJson = string.Empty;
                tsFhirModel.LookUpAndTranslateJson = string.Empty;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                tsFhirModel.observationJson = string.Empty;
                tsFhirModel.LookUpAndTranslateJson = string.Empty;
                StateHasChanged();
            }
        }

        private async Task<string> TranslateObservationCodes(List<JToken> sourceCodes)
        {
            JArray originalCodings = new JArray();
            JArray translatedCodings = new JArray();
            try
            {
                foreach (JToken codeToken in sourceCodes)
                {
                    foreach (var sourceCoding in codeToken)
                    {
                        var system = (string)sourceCoding["system"];
                        if (!string.IsNullOrEmpty(system) && system.Contains("snomed"))
                        {
                            originalCodings.Add(sourceCoding);
                            var codeType = (string)sourceCoding["code"];
                            var translateResponse = await apmService.TranslateCode(codeType, system, "http://www.ama-assn.org/go/cpt");
                            if (translateResponse.IsSuccessStatusCode)
                            {
                                var translateJsonResponse = translateResponse.Content.ReadAsStringAsync().Result;
                                JObject translatedObject = JObject.Parse(translateJsonResponse);
                                var targetCoding = GetAllCodes(translatedObject, "valueCoding");
                                Console.WriteLine(targetCoding);
                                JObject transcode = targetCoding[0] as JObject;
                                transcode.Remove("id");
                                translatedCodings.Add(transcode);
                                // sourceCoding.AddAfterSelf(translatedCodings);
                            }
                        }
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception");
                Console.WriteLine(e.Message); //manage the cancel search
            }
            return sourceCodes.ToString();
        }

        private async Task<JObject> TranslateCode(JToken sourceCode)
        {
            JObject translatedCode = new JObject();
            try
            {
                if (Convert.ToString(sourceCode["system"]).Contains("snomed"))
                {
                    var translateResponse = await apmService.TranslateCode((string)sourceCode["code"], (string)sourceCode["system"], "http://www.ama-assn.org/go/cpt");
                    var system = (string)sourceCode["system"];
                    if (translateResponse.IsSuccessStatusCode)
                    {
                        var translateJsonResponse = translateResponse.Content.ReadAsStringAsync().Result;
                        JObject translatedObject = JObject.Parse(translateJsonResponse);
                        var targetCode = GetAllCodes(translatedObject, "valueCoding");
                        Console.WriteLine(targetCode);
                        translatedCode = targetCode[0] as JObject;
                        translatedCode.Remove("id");
                    }
                }
                if (Convert.ToString(sourceCode["system"]).Contains("local-codes"))
                {
                    var systemurl = "microsoft_ehr1_labs";
                    var translateResponse = await apmService.TranslateCode((string)sourceCode["code"], systemurl, "loinc");

                    if (translateResponse.IsSuccessStatusCode)
                    {
                        var translateJsonResponse = translateResponse.Content.ReadAsStringAsync().Result;
                        JObject translatedObject = JObject.Parse(translateJsonResponse);
                        var targetCode = GetAllCodes(translatedObject, "valueCoding");
                        Console.WriteLine(targetCode);
                        translatedCode = targetCode[0] as JObject;
                        translatedCode.Remove("id");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception");
                Console.WriteLine(e.Message); //manage the cancel search
            }

            return translatedCode;

        }

        private List<JToken> GetAllCodes(JObject sourceObject, string elementName)
        {
            var results = new List<JToken>();
            try
            {
                foreach (JToken token in sourceObject.Descendants())
                {
                    if (token.Type == JTokenType.Property)
                    {
                        JProperty property = (JProperty)token;

                        if (property.Name.Equals(elementName))
                        {
                            results.Add(property.Value);
                        }
                    }
                }
            }
            catch
            {
                throw;
            }

            return results;
        }

        private void AddToObject(string path, JObject originalObject, JObject objectToAdd)
        {
            JObject codeObject = (JObject)originalObject.SelectToken(path);
            if (codeObject != null)
            {
                codeObject.AddAfterSelf(objectToAdd);
            }
            else
            {
                throw new ArgumentException("Array not found in JObject.");
            }
        }


        async void ResetAllObservationsModal()
        {
            try
            {
                var messageForm = Modal.Show<Confirm>("Reset All");
                var result = await messageForm.Result;
                Console.WriteLine(result.Data.ToString());
                if (result.Data.ToString() == "Ok")
                {
                    this.ResetObservations();
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception");
                Console.WriteLine(ex.Message);
            }

        }
    }
}
