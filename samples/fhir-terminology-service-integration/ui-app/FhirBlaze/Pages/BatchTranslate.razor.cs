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

        [Inject]
        IJSRuntime runtime { get; set; }

        [CascadingParameter] public IModalService Modal { get; set; } = default!;

        protected override async void OnInitialized()
        {

            base.OnInitialized();
            if (tsFhirModel == null)
            {
                tsFhirModel = new TSFhirModel();
            }

        }

        public async void GetBatchDetails(string codeSystem)
        {
            try
            {
                // fetch data from json and display to ui 
                var observations = await Http.GetStringAsync("data/BatchTranslateData.json");
                JObject bundleObject = JObject.Parse(observations);
                JObject codeObject = bundleObject.GetValue(codeSystem) as JObject;
                if (codeObject != null)
                {
                    tsFhirModel.observationJson = codeObject.ToString();
                    StateHasChanged();
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
                JObject bundleObject = JObject.Parse(tsFhirModel.observationJson);
                var translatedCode = await apmService.BatchTranslateCode(bundleObject.ToString());
                if (translatedCode.IsSuccessStatusCode)
                {
                    var translateJsonResponse = translatedCode.Content.ReadAsStringAsync().Result;
                    JObject translatedJobject = JObject.Parse(translateJsonResponse);
                    tsFhirModel.LookUpAndTranslateJson = translatedJobject.ToString();
                    StateHasChanged();
                }
                else
                {
                    Console.WriteLine("Error Code :" + translatedCode.StatusCode.ToString());
                    Console.WriteLine(translatedCode.Content.ReadAsStringAsync().Result);
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
        
    }
}
