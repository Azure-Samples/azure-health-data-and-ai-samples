@description('Base name used for generating resource names.')
param name string

@description('Location for the function app.')
param location string

@description('Shared tags for all resources.')
param appTags object

@description('Microsoft Entra Id tenant ID for the FHIR Service.')
param tenantId string

@description('Azure B2C Directory tenant ID.')
param b2cTenantId string

@description('Microsoft Entra Id/B2c Application ID for the FHIR resource application.')
param fhirResourceAppId string

@description('Microsoft Entra Id/B2c Application ID for the Standalone Client application.')
param standaloneAppClientId string 

@description('Azure B2C Directory authority url.')
param authorityUrl string

@description('Condition to include B2C in SMART on FHIR')
param smartOnFhirWithB2C bool

@description('Name of the Azure API Management instance.')
param apimName string

@description('URL used to access the SMART on FHIR frontend application.')
param smartFrontendAppUrl string

@description('Audience used to access the FHIR Service by the custom operation. (Optional, defaults to fhirUrl if not specified.)')
param fhirServiceAudience string

@description('Microsoft Entra ID Application ID for the context application.')
param contextAadApplicationId string

@description('App Insights Instrumentation Key for the sample. (Optional)')
param appInsightsInstrumentationKey string

@description('App Insights Connection String for the sample. (Optional)')
param appInsightsConnectionString string

@description('Name for the storage account needed for Custom Operation Function Apps')
param customOperationsFuncStorName string

@description('Azure Resource ID for the Function App hosting plan.')
param hostingPlanId string

@description('Key Vault for storing application registrations secret')
param keyVaultName string

param redisCacheId string
param redisCacheHostName string
param redisApiVersion string

@description('Name for the Function App to deploy the SDK custom operations to.')
var authCustomOperationsFunctionAppName = '${name}-aad-func'


@description('Used for Custom Operation Azure Function App temp storage and auth.')
resource funcStorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' existing = {
  name: customOperationsFuncStorName

  resource blobService 'blobServices@2021-06-01' = {
    name: 'default'
  }
}

@description('Azure Function used to run auth flow custom operations using the Azure Health Data Services Toolkit')
resource authCustomOperationFunctionApp 'Microsoft.Web/sites@2021-03-01' = {
  name: authCustomOperationsFunctionAppName
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
      cors: {
        allowedOrigins: [
          smartFrontendAppUrl
        ]
      }
    }
    
  }

  tags: union(appTags, {'azd-service-name': 'auth'})
}

var functionConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${funcStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorageAccount.listKeys().keys[0].value}'
var redisPrimaryKey = listKeys(redisCacheId, redisApiVersion).primaryKey
var redisConnectionString = '${redisCacheHostName},password=${redisPrimaryKey},ssl=True,abortConnect=False'

resource authCustomOperationAppSettings 'Microsoft.Web/sites/config@2020-12-01' = {
  name: 'appsettings'
  parent: authCustomOperationFunctionApp
  properties: {
    AzureWebJobsStorage: functionConnectionString
    // WEBSITE_CONTENTAZUREFILECONNECTIONSTRING: 'DefaultEndpointsProtocol=https;AccountName=${funcStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorageAccount.listKeys().keys[0].value}'
    // WEBSITE_CONTENTSHARE: authCustomOperationsFunctionAppName
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    APPINSIGHTS_INSTRUMENTATIONKEY: appInsightsInstrumentationKey
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
    SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'
    ENABLE_ORYX_BUILD: 'true'

    AZURE_ApiManagementHostName: '${apimName}.azure-api.net'
    AZURE_APPINSIGHTS_INSTRUMENTATIONKEY: appInsightsInstrumentationKey
    AZURE_APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
    AZURE_TenantId: tenantId
    AZURE_SmartonFhir_with_B2C: '${smartOnFhirWithB2C}'
    AZURE_Authority_URL: authorityUrl
    AZURE_B2C_Tenant_Id: b2cTenantId
    AZURE_Standalone_App_ClientId: standaloneAppClientId
    AZURE_Fhir_Resource_AppId: fhirResourceAppId
    AZURE_KeyVaultName: keyVaultName
    AZURE_FhirAudience: fhirServiceAudience
    AZURE_ContextAppClientId: contextAadApplicationId
    AZURE_CacheConnectionString: redisConnectionString
    AZURE_Debug: 'true'
  }
}

output functionAppUrl string = 'https://${authCustomOperationFunctionApp.properties.defaultHostName}/api'
output functionAppPrincipalId string = authCustomOperationFunctionApp.identity.principalId
output authCustomOperationAudience string = fhirServiceAudience
output cacheConnectionString string = redisConnectionString
