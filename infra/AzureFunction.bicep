param storageAccountName string
param storagekey string
param appServiceName string
param functionAppName string
param appInsightsInstrumentationKey string
param location string
param hl7FilesContinerName string
param hl7ValidatedBlobContainer string
param hl7validationfailContainer string
param hl7resyncontainer string
param hl7ConvertedContainer string
param hl7conversionfailcontainer string
param hl7SuccessBlobContainer string
param hl7FailedBlobContainer string
param hl7SkippedBlobContainer string
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
    AZURE_BlobContainer:hl7FilesContinerName
    AZURE_ValidatedBlobContainer:hl7ValidatedBlobContainer
    AZURE_Hl7validationfailBlobContainer:hl7validationfailContainer
    AZURE_Hl7ResynchronizationContainer:hl7resyncontainer
    AZURE_ConvertedContainer:hl7ConvertedContainer
    AZURE_ConversionfailContainer:hl7conversionfailcontainer
    AZURE_SkippedBlobContainer:hl7SkippedBlobContainer
    AZURE_FailedBlobContainer:hl7FailedBlobContainer    
    AZURE_ProcessedBlobContainer:hl7SuccessBlobContainer
    AZURE_HL7FailedBlob:'hl7'
    AZURE_FhirFailedBlob:'fhir'
    AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storagekey}'
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'    
  }, functionSettings)
}

output functionAppName string = functionAppName
output functionAppPrincipalId string = functionApp.identity.principalId
output hostName string = functionApp.properties.defaultHostName
output functionkey string = listkeys('${functionApp.id}/host/default', '2016-08-01').functionKeys.default
output functionURL string = 'https://${functionApp.properties.defaultHostName}'
