@description('Base name used for generating resource names.')
param name string

@description('Location for the function app.')
param location string

@description('Shared tags for all resources.')
param appTags object

@description('Azure Active Directory tenant ID for the FHIR Service.')
param tenantId string

@description('Name of the Azure API Management instance.')
param apimName string

@description('URL used to access the FHIR Service by the custom operation.')
param fhirUrl string

@description('App Insights Instrumentation Key for the sample. (Optional)')
param appInsightsInstrumentationKey string

@description('App Insights Connection String for the sample. (Optional)')
param appInsightsConnectionString string

@description('Name for the storage account needed for Custom Operation Function Apps')
param customOperationsFuncStorName string

@description('Azure Resource ID for the Function App hosting plan.')
param hostingPlanId string

@description('Name for the Function App to deploy the Export Custom Operations to')
var exportCustomOperationsFunctionAppName = '${name}-exp-func'

@description('Used for Custom Operation Azure Function App temp storage and auth.')
resource funcStorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' existing = {
  name: customOperationsFuncStorName
}

resource funcTableService 'Microsoft.Storage/storageAccounts/tableServices@2022-05-01' = {
  name: 'default'
  parent: funcStorageAccount
}

resource symbolicname 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-05-01' = {
  name: 'jwksBackendService'
  parent: funcTableService
}

@description('Azure Function used to run export custom operations using the Azure Health Data Services Toolkit')
resource exportCustomOperationFunctionApp 'Microsoft.Web/sites@2021-03-01' = {
  name: exportCustomOperationsFunctionAppName
  location: location
  kind: 'functionapp,linux'

  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    httpsOnly: true
    enabled: true
    serverFarmId: hostingPlanId
    reserved: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'dotnet-isolated|6.0'
      use32BitWorkerProcess: false
    }
    
  }

  tags: union(appTags, {'azd-service-name': 'export'})
}

resource exportCustomOperationFfhirProxyAppSettings 'Microsoft.Web/sites/config@2020-12-01' = {
  name: 'appsettings'
  parent: exportCustomOperationFunctionApp
  properties: {
    AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${funcStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorageAccount.listKeys().keys[0].value}'
    // WEBSITE_CONTENTAZUREFILECONNECTIONSTRING: 'DefaultEndpointsProtocol=https;AccountName=${funcStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorageAccount.listKeys().keys[0].value}'
    // WEBSITE_CONTENTSHARE: exportCustomOperationsFunctionAppName
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    APPINSIGHTS_INSTRUMENTATIONKEY: appInsightsInstrumentationKey
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString    
    SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'
    ENABLE_ORYX_BUILD: 'true'
    
    AZURE_ApiManagementHostName: '${apimName}.azure-api.net'
    AZURE_FhirServerUrl: fhirUrl
    AZURE_APPINSIGHTS_INSTRUMENTATIONKEY: appInsightsInstrumentationKey
    AZURE_APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
    AZURE_TenantId: tenantId
    AZURE_ExportStorageAccountUrl: 'https://${funcStorageAccount}.blob.${environment().suffixes.storage}'
    AZURE_Debug: 'true'
  }
}

output functionAppUrl string = 'https://${exportCustomOperationFunctionApp.properties.defaultHostName}/api'
output functionAppPrincipalId string = exportCustomOperationFunctionApp.identity.principalId
