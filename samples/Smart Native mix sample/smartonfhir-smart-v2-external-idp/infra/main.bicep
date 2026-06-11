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

@description('Microsoft Entra Application (client) ID representing the FHIR resource API. Used to configure the custom fhirUser claim and data loading scripts. Only meaningful when IdpType is EntraId.')
param FhirResourceAppId string = ''

@description('Name of the Log Analytics workspace. Leave blank to auto-generate.')
param logAnalyticsName string = ''

@description('Upstream IdP integration mode. EntraId proxies authorize/token to Microsoft Entra; ExternalIdp forwards to an external IdP (e.g. Okta). When EntraId, a backend services Key Vault is also provisioned.')
@allowed([
  'EntraId'
  'ExternalIdp'
])
param IdpType string = 'EntraId'

@description('Microsoft Entra tenant id. Leave blank to use the deployment subscription tenant. Used only when IdpType is EntraId.')
param TenantId string = ''

@description('External IDP authority URL (example: https://your-okta-domain/oauth2/default). Required only when IdpType is ExternalIdp.')
param AuthorityURL string = ''

@description('Azure AD object ID of the deployer. Granted Key Vault Secrets Officer on the backend services KV (EntraId mode only). Provided automatically by azd as AZURE_PRINCIPAL_ID.')
param principalId string = ''

@description('Claim name in access token containing user identifier. Leave blank to auto-derive: oid for EntraId, sub for ExternalIdp. Set explicitly only for non-standard IdPs.')
param UserIdClaimType string = ''

@description('Optional Redis-compatible connection string for distributed EHR launch context cache (e.g. Azure Managed Redis). Leave blank to use in-memory caching inside the Function App (suitable for samples and single-instance deployments).')
param CacheConnectionString string = ''

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
var tenantIdResolved = empty(TenantId) ? subscription().tenantId : TenantId
var userIdClaimTypeResolved = empty(UserIdClaimType) ? (IdpType == 'EntraId' ? 'oid' : 'sub') : UserIdClaimType
var deployBackendVault = IdpType == 'EntraId'
var backendVaultName = '${name}-bk-kv'

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
    idpType: IdpType
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
    cacheConnectionString: CacheConnectionString
    idpType: IdpType
    tenantId: tenantIdResolved
    authorityUrl: AuthorityURL
    userIdClaimType: userIdClaimTypeResolved
    backendServiceVaultName: deployBackendVault ? backendVaultName : ''
    fhirResourceAppId: FhirResourceAppId
  }
}

module backendVault './core/keyVault.bicep' = if (deployBackendVault) {
  name: 'backendVaultDeploy'
  scope: resourceGroup(resourceGroupName)
  params: {
    keyVaultName: backendVaultName
    location: location
    appTags: appTags
    writerObjectIds: empty(principalId) ? [] : [ principalId ]
    readerObjectIds: [ authCustomOperation.outputs.functionAppPrincipalId ]
  }
}

@description('Grant the deployer FHIR Data Contributor on the FHIR service so test data load and direct FHIR calls work without manual role assignment.')
module fhirContributorForDeployer './core/identity.bicep' = if (!empty(principalId)) {
  name: 'fhirContributorForDeployer'
  scope: resourceGroup(resourceGroupName)
  params: {
    fhirId: fhir.outputs.fhirId
    principalId: principalId
    principalType: 'User'
    roleType: 'fhirContributor'
  }
}

output AZURE_RESOURCE_GROUP string = resourceGroupName
output FhirUrl string = fhirUrl
output FhirAudience string = fhirAudienceResolved
output FhirResourceAppId string = FhirResourceAppId
output TenantId string = tenantIdResolved
output FunctionBaseUrl string = authCustomOperation.outputs.functionAppUrl
output FunctionAppManagedIdentityPrincipalId string = authCustomOperation.outputs.functionAppPrincipalId
output CacheConnectionString string = CacheConnectionString
output BackendServiceKeyVaultName string = backendVault.?outputs.keyVaultName ?? ''
output BackendServiceKeyVaultUri string = backendVault.?outputs.keyVaultUri ?? ''
