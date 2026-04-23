targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Environment name used as resource prefix.')
param name string

@minLength(1)
@description('Primary deployment location.')
param location string

@description('Client ID for context cache caller validation (optional).')
param ContextAppClientId string = ''

@description('Audience for SMART scopes. Leave blank to use FHIR URL.')
param FhirAudience string = ''

@description('Name of the Log Analytics workspace. Leave blank to auto-generate.')
param logAnalyticsName string = ''

@description('External IDP authority URL (example: https://your-okta-domain/oauth2/default).')
param AuthorityURL string

@description('Claim name in access token containing user identifier.')
param UserIdClaimType string = 'sub'

var nameClean = replace(name, '-', '')
var nameCleanShort = length(nameClean) > 16 ? substring(nameClean, 0, 16) : nameClean
var appTags = {
  AppID: 'fhir-smart-onfhir-external-idp'
  'azd-env-name': name
}

var workspaceNameResolved = '${nameCleanShort}health'
var fhirNameResolved = 'fhirdata'
var fhirUrl = 'https://${workspaceNameResolved}-${fhirNameResolved}.fhir.azurehealthcareapis.com'
var fhirAudienceResolved = empty(FhirAudience) ? fhirUrl : FhirAudience

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: '${name}-rg'
  location: location
  tags: appTags
}
var resourceGroupName = rg.name
var appInsightsName = '${nameCleanShort}-appins'
var logAnalyticsNameResolved = length(logAnalyticsName) > 0 ? logAnalyticsName : '${nameCleanShort}-la'

module fhir 'core/fhir.bicep' = {
  name: 'fhirDeploy'
  scope: resourceGroup(resourceGroupName)
  params: {
    createWorkspace: true
    createFhirService: true
    workspaceName: workspaceNameResolved
    fhirServiceName: fhirNameResolved
    location: location
    appTags: appTags
    audience: fhirAudienceResolved
    AuthorityURL: AuthorityURL
  }
}

module monitoring 'core/monitoring.bicep' = {
  name: 'monitoringDeploy'
  scope: resourceGroup(resourceGroupName)
  params: {
    logAnalyticsName: logAnalyticsNameResolved
    appInsightsName: appInsightsName
    location: location
    appTags: appTags
  }
}

module functionBase 'core/functionHost.bicep' = {
  name: 'functionBaseDeploy'
  scope: resourceGroup(resourceGroupName)
  params: {
    appTags: appTags
    location: location
    name: name
    nameCleanShort: nameCleanShort
  }
}

module redis './core/redisCache.bicep' = {
  name: 'redisDeploy'
  scope: resourceGroup(resourceGroupName)
  params: {
    namePrefix: name
    location: location
  }
}

module authCustomOperation './app/authCustomOperation.bicep' = {
  name: 'authCustomOperationDeploy'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: name
    location: location
    appTags: appTags
    fhirServiceUrl: fhirUrl
    fhirServiceAudience: fhirAudienceResolved
    contextAadApplicationId: ContextAppClientId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    customOperationsFuncStorName: functionBase.outputs.storageAccountName
    hostingPlanId: functionBase.outputs.hostingPlanId
    redisCacheId: redis.outputs.redisCacheId
    redisApiVersion: redis.outputs.redisApiVersion
    redisCacheHostName: redis.outputs.redisCacheHostName
    authorityUrl: AuthorityURL
    userIdClaimType: UserIdClaimType
  }
}

output AZURE_RESOURCE_GROUP string = resourceGroupName
output TenantId string = subscription().tenantId
output FhirUrl string = fhirUrl
output FhirAudience string = fhirAudienceResolved
output FunctionBaseUrl string = authCustomOperation.outputs.functionAppUrl
output FunctionAppManagedIdentityPrincipalId string = authCustomOperation.outputs.functionAppPrincipalId
output CacheConnectionString string = authCustomOperation.outputs.cacheConnectionString
