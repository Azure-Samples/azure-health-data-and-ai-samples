@description('Base name used for generating resource names.')
param name string

@description('Location for the function app.')
param location string

@description('Shared tags for all resources.')
param appTags object

@description('Upstream IdP integration mode. EntraId proxies authorize/token to Microsoft Entra; ExternalIdp forwards to an external IdP (e.g. Okta).')
@allowed([
  'EntraId'
  'ExternalIdp'
])
param idpType string = 'EntraId'

@description('Microsoft Entra tenant id. Required when idpType is EntraId.')
param tenantId string = ''

@description('External IDP authority URL (e.g. https://your-okta-domain/oauth2/default). Required when idpType is ExternalIdp.')
param authorityUrl string = ''

@description('Key Vault name hosting backend service client secrets. Empty disables SMART v2 Backend Services on the proxy.')
param backendServiceVaultName string = ''

@description('Claim type to use for identifying user in access token.')
param userIdClaimType string

@description('FHIR server base URL used by function for SMART config discovery.')
param fhirServiceUrl string

@description('Audience used to access the FHIR Service by the custom operation. (Optional, defaults to fhirUrl if not specified.)')
param fhirServiceAudience string

@description('Microsoft Entra ID Application ID for the context application.')
param contextAadApplicationId string

@description('Microsoft Entra Application (client) ID representing the FHIR resource API. Exposed as an app setting for downstream tooling (custom fhirUser claim, data loading). Only meaningful when idpType is EntraId.')
param fhirResourceAppId string = ''

@description('App Insights Connection String for the sample. (Optional)')
param appInsightsConnectionString string

@description('Name for the storage account needed for Custom Operation Function Apps')
param customOperationsFuncStorName string

@description('Azure Resource ID for the Function App hosting plan.')
param hostingPlanId string

@description('Optional Redis-compatible connection string for distributed EHR launch context cache. Leave blank to use in-memory caching.')
param cacheConnectionString string = ''

@description('Name for the Function App to deploy custom operations.')
var authCustomOperationsFunctionAppName = '${name}-auth-func'


@description('Used for Custom Operation Azure Function App temp storage and auth.')
resource funcStorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' existing = {
  name: customOperationsFuncStorName

  resource blobService 'blobServices@2021-06-01' = {
    name: 'default'
  }
}

var siteConfig = {
  linuxFxVersion: 'dotnet-isolated|8.0'
  use32BitWorkerProcess: false
  cors: {}
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
    siteConfig: siteConfig
  }

  tags: union(appTags, {'azd-service-name': 'auth'})
}

var functionConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${funcStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorageAccount.listKeys().keys[0].value}'

resource authCustomOperationAppSettings 'Microsoft.Web/sites/config@2020-12-01' = {
  name: 'appsettings'
  parent: authCustomOperationFunctionApp
  properties: {
    AzureWebJobsStorage: functionConnectionString
    // WEBSITE_CONTENTAZUREFILECONNECTIONSTRING: 'DefaultEndpointsProtocol=https;AccountName=${funcStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorageAccount.listKeys().keys[0].value}'
    // WEBSITE_CONTENTSHARE: authCustomOperationsFunctionAppName
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
    SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'
    ENABLE_ORYX_BUILD: 'true'

    AZURE_APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
    AZURE_IdpType: idpType
    AZURE_TenantId: tenantId
    AZURE_Authority_URL: authorityUrl
    AZURE_FhirServerUrl: fhirServiceUrl
    AZURE_FhirAudience: fhirServiceAudience
    AZURE_FhirResourceAppId: fhirResourceAppId
    AZURE_UserIdClaimType: userIdClaimType
    AZURE_ContextAppClientId: contextAadApplicationId
    AZURE_CacheConnectionString: cacheConnectionString
    AZURE_BackendServiceKeyVaultStore: backendServiceVaultName
    AZURE_Debug: 'true'
  }
}

output functionAppUrl string = 'https://${authCustomOperationFunctionApp.properties.defaultHostName}/api'
output functionAppPrincipalId string = authCustomOperationFunctionApp.identity.principalId
output authCustomOperationAudience string = fhirServiceAudience
