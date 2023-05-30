using FhirBlaze.Model;
using FhirBlaze.SharedComponents.Services;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;


namespace FhirBlaze.Pages
{
    public partial class Index
    {

        [Inject]
        IAPIMService apmService { get; set; }

        [Inject]
        SystemUrl systemUrl { get; set; }

        [Parameter]
        public TSFhirModel? tsFhirModel { get; set; }

        [Parameter]
        public LookUpTableModel LookUpTblObj { get; set; }

        [Parameter]
        public TranslateTableModel TranslateTblObj { get; set; }

        protected override void OnInitialized()
        {
            if (tsFhirModel == null)
            {
                tsFhirModel = new TSFhirModel();
            }
        }

        public async void GetLookUpCode()
        {
            try
            {
                var lookupJson = await apmService.GetLookUpCode(tsFhirModel.LookupCodeValue, tsFhirModel.LookupCodeSystem);
                if (lookupJson.IsSuccessStatusCode)
                {

                    var lookupJsonResponse = lookupJson.Content.ReadAsStringAsync().Result;
                    JObject jObject = JObject.Parse(lookupJsonResponse);

                    tsFhirModel.LookUpAndTranslateJson = jObject.ToString();
                    if (!string.IsNullOrEmpty(tsFhirModel.LookUpAndTranslateJson))
                    {
                        TranslateTblObj = null;
                        LookUpTblObj = new LookUpTableModel();
                        LookUpTblObj.Code = tsFhirModel.LookupCodeValue;
                        LookUpTblObj.System = (string)jObject["parameter"][0]["valueString"];
                        LookUpTblObj.Description = (string)jObject["parameter"][1]["valueString"];
                    }

                    StateHasChanged();
                }
                else
                {
                    tsFhirModel.LookUpAndTranslateJson = "No Lookup code found";
                    TranslateTblObj = null;
                    LookUpTblObj = null;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                tsFhirModel.LookUpAndTranslateJson = ex.Message;
                TranslateTblObj = null;
                LookUpTblObj = null;
                Console.WriteLine(ex.Message);
            }
        }

        public async void TranslateCode()
        {
            try
            {
                var translateJson = await apmService.TranslateCode(tsFhirModel.TranslateCodeValue, tsFhirModel.TranslateSourceCodeSystem, tsFhirModel.TranslateTargetCodeSystem);
                if (translateJson.IsSuccessStatusCode)
                {
                    var translateJsonResponse = translateJson.Content.ReadAsStringAsync().Result;
                    JObject jObject = JObject.Parse(translateJsonResponse);

                    tsFhirModel.LookUpAndTranslateJson = jObject.ToString();
                    if (!string.IsNullOrEmpty(tsFhirModel.LookUpAndTranslateJson))
                    {
                        var paths = jObject
                             .Descendants()
                             .Where(x => x is JObject)
                             .Where(x => (string)x["name"] == "match")
                             .Select(x => x.Path)
                             .ToList();

                        if (paths.Any())
                        {
                            foreach (var path in paths)
                            {
                                var TranslateJObj = jObject.SelectToken(path);
                                
                                if (TranslateJObj != null)
                                {
                                    LookUpTblObj = null;
                                    TranslateTblObj = new();
                                    TranslateTblObj.SourceCode = tsFhirModel.TranslateCodeValue;
                                    TranslateTblObj.SourceSystem = tsFhirModel.TranslateSourceCodeSystem;
                                    TranslateTblObj.TargetCode = TranslateJObj.SelectToken("part[0].valueCoding.code").ToString();
                                    TranslateTblObj.TargetSystem = TranslateJObj.SelectToken("part[0].valueCoding.system").ToString();
                                    TranslateTblObj.TargetDescription = TranslateJObj.SelectToken("part[0].valueCoding.display").ToString();
                                }
                            }
                        }
                    }
                    StateHasChanged();
                }
                else
                {
                    tsFhirModel.LookUpAndTranslateJson = "Translate code failed";
                    TranslateTblObj = null;
                    LookUpTblObj = null;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                tsFhirModel.LookUpAndTranslateJson = ex.Message;
                TranslateTblObj = null;
                LookUpTblObj = null;
                Console.WriteLine(ex.Message);
            }
        }

    }
}
