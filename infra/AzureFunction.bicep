param storageAccountName string
param storagekey string
param appServiceName string
param functionAppName string
param appInsightsInstrumentationKey string
param location string
param hl7containername string
param ValidatedBlobContainer string 
param Hl7validationfailBlobContainer string 
param Hl7skippedContainer string 
param Hl7ResynchronizationContainer string 
param ConvertedContainer string 
param ConversionfailContainer string 
param Hl7ConverterJsonContainer string
param Hl7PostProcessContainer string 
param processedblobcontainer string 
param HL7FailedBlob string 
param FailedBlobContainer string 
param FhirFailedBlob string 
param SkippedBlobContainer string 
param FhirJsonContainer string 
param ValidatedContainer string
param HL7FhirPostPorcessJson string 


param functionSettings object = {}
param appTags object = {}


@description('App Service used to run Azure Function')
resource hostingPlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: appServiceName
  location: location
  kind: 'functionapp'
  sku: {
    name: 'S1'
  }
  properties: {}
  tags: appTags
}

@description('Azure Function used to run validation')
resource functionApp 'Microsoft.Web/sites@2021-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'

  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    httpsOnly: true
    enabled: true
    serverFarmId: hostingPlan.id
    clientAffinityEnabled: false
    siteConfig: {
      alwaysOn:true
    }
  }

  tags: union(appTags, {
    'azd-service-name': 'func'
  })
}

resource fhirProxyAppSettings 'Microsoft.Web/sites/config@2020-12-01' = {
  name: 'appsettings'
  parent: functionApp
  properties: union({
    APPINSIGHTS_INSTRUMENTATIONKEY: appInsightsInstrumentationKey
    APPLICATIONINSIGHTS_CONNECTION_STRING: 'InstrumentationKey=${appInsightsInstrumentationKey}'
    AZURE_BlobConnectionString:'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storagekey}'
    AZURE_BlobContainer:hl7containername
    AZURE_ValidatedBlobContainer:ValidatedBlobContainer
    AZURE_Hl7validationfailBlobContainer:Hl7validationfailBlobContainer
    AZURE_Hl7skippedContainer:Hl7skippedContainer
    AZURE_Hl7ResynchronizationContainer:Hl7ResynchronizationContainer
    AZURE_ConvertedContainer:ConvertedContainer
    AZURE_ConversionfailContainer:ConversionfailContainer
    AZURE_Hl7ConverterJsonContainer:Hl7ConverterJsonContainer
    AZURE_Hl7PostProcessContainer:Hl7PostProcessContainer    
    AZURE_ProcessedBlobContainer:processedblobcontainer
    AZURE_HL7FailedBlob:HL7FailedBlob
    AZURE_AZURE_FailedBlobContainer:FailedBlobContainer
    AZURE_FhirFailedBlob:FhirFailedBlob
    AZURE_SkippedBlobContainer:SkippedBlobContainer
    AZURE_FhirJsonContainer:FhirJsonContainer
    AZURE_ValidatedContainer:ValidatedContainer
    AZURE_HL7FhirPostPorcessJson:HL7FhirPostPorcessJson
    AZURE_Hl7validationMaxParallelism:'250'
    AZURE_HL7SequencingMaxParallelism:'250'
    AZURE_HL7ConverterMaxParallelism:'940'
    AZURE_FHIRPostProcessMaxParallelism:'25'
    AZURE_UploadFhirJsonMaxParallelism:'740'
    AZURE_HttpFailStatusCodes:'408,429,500,501,502,503,504,505,506,507,508,510,511'
    AzureFunctionsJobHost__functionTimeout:'23:00:00'
    AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storagekey}'
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'    
  }, functionSettings)
}

var key = listkeys('${functionApp.id}/host/default', '2016-08-01').functionKeys.default

output functionAppName string = functionAppName
output functionAppPrincipalId string = functionApp.identity.principalId
output hostName string = functionApp.properties.defaultHostName
output functionkey string = key
output functionURL string = 'https://${functionApp.properties.defaultHostName}'
