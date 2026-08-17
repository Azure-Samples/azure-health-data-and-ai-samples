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
param IdpType string

@description('Microsoft Entra tenant id. Leave blank to use the deployment subscription tenant. Used only when IdpType is EntraId.')
param TenantId string = ''

@description('External IDP authority URL (example: https://your-okta-domain/oauth2/default). Required only when IdpType is ExternalIdp.')
param AuthorityURL string = ''

@description('Azure AD object ID of the deployer. Granted Key Vault Secrets Officer on the backend services KV (EntraId mode only). Provided automatically by azd as AZURE_PRINCIPAL_ID.')
param principalId string = ''

@description('Optional Redis-compatible connection string for distributed EHR launch context cache (e.g. Azure Managed Redis). Leave blank to use in-memory caching inside the Function App (suitable for samples and single-instance deployments).')
param CacheConnectionString string = ''

@description('Full resource ID of an existing AHDS FHIR service to reuse. Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.HealthcareApis/workspaces/{ws}/fhirservices/{svc}. Leave blank to create a new workspace + FHIR service. When reused, this deployment does NOT modify the existing FHIR service authenticationConfiguration; the caller is responsible for ensuring audience, authority and smartIdentityProviders are correctly configured for the chosen IdpType.')
param ExistingFhirServiceId string = ''

@maxLength(24)
@description('Override the backend services Key Vault name (3-24 chars, KV naming rules). Leave blank to auto-generate as "<nameCleanShort>-bk-kv". Set this to the existing vault name when upgrading an environment that was originally deployed with a different naming convention, so previously-provisioned client secrets are preserved instead of being stranded in an orphaned vault. Only used when IdpType is EntraId.')
param backendVaultName string = ''

var nameClean = replace(name, '-', '')
var nameCleanShort = length(nameClean) > 16 ? substring(nameClean, 0, 16) : nameClean
var appTags = {
  AppID: 'fhir-smart-onfhir-external-idp'
  'azd-env-name': name
}

// Parse existing FHIR service id (when provided) to derive RG / workspace / service names.
// Expected id shape: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.HealthcareApis/workspaces/{ws}/fhirservices/{svc}
// split() yields: ['', 'subscriptions', <sub>, 'resourceGroups', <rg>, 'providers', 'Microsoft.HealthcareApis', 'workspaces', <ws>, 'fhirservices', <svc>]
var reuseFhir = !empty(ExistingFhirServiceId)
var fhirIdParts = split(ExistingFhirServiceId, '/')
var existingFhirRg = reuseFhir ? fhirIdParts[4] : ''
var existingWorkspaceName = reuseFhir ? fhirIdParts[8] : ''
var existingFhirServiceName = reuseFhir ? fhirIdParts[10] : ''

var workspaceNameResolved = reuseFhir ? existingWorkspaceName : '${nameCleanShort}health'
var fhirNameResolved = reuseFhir ? existingFhirServiceName : 'fhirdata'
var fhirUrl = 'https://${workspaceNameResolved}-${fhirNameResolved}.fhir.azurehealthcareapis.com'
var fhirAudienceResolved = empty(FhirAudience) ? fhirUrl : FhirAudience
var tenantIdResolved = empty(TenantId) ? subscription().tenantId : TenantId
var deployBackendVault = IdpType == 'EntraId'
// Prefer an explicit override to preserve secrets on upgrades; otherwise auto-generate from
// nameCleanShort so we stay within Key Vault's 24-char name limit even for long env names.
var backendVaultNameResolved = empty(backendVaultName) ? '${nameCleanShort}-bk-kv' : backendVaultName

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: '${name}-rg'
  location: location
  tags: appTags
}
var resourceGroupName = rg.name
var fhirRgName = reuseFhir ? existingFhirRg : resourceGroupName
var appInsightsName = '${nameCleanShort}-appins'
var logAnalyticsNameResolved = length(logAnalyticsName) > 0 ? logAnalyticsName : '${nameCleanShort}-la'

module fhir 'core/fhir.bicep' = {
  name: 'fhirDeploy'
  scope: resourceGroup(fhirRgName)
  params: {
    createWorkspace: !reuseFhir
    createFhirService: !reuseFhir
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
    backendServiceVaultName: deployBackendVault ? backendVaultNameResolved : ''
    fhirResourceAppId: FhirResourceAppId
  }
}

module backendVault './core/keyVault.bicep' = if (deployBackendVault) {
  name: 'backendVaultDeploy'
  scope: resourceGroup(resourceGroupName)
  params: {
    keyVaultName: backendVaultNameResolved
    location: location
    appTags: appTags
    writerObjectIds: empty(principalId) ? [] : [ principalId ]
    readerObjectIds: [ authCustomOperation.outputs.functionAppPrincipalId ]
  }
}

@description('Grant the deployer FHIR Data Contributor on the newly-created FHIR service so test data load and direct FHIR calls work without a manual role assignment. Skipped when reusing an existing FHIR service (ExistingFhirServiceId set) to keep reuse mode strictly non-destructive on the caller FHIR; grant the role manually if the deployer needs direct data-plane access.')
module fhirContributorForDeployer './core/identity.bicep' = if (!empty(principalId) && !reuseFhir) {
  name: 'fhirContributorForDeployer'
  scope: resourceGroup(fhirRgName)
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
output FhirResourceGroup string = fhirRgName
output FhirServiceId string = fhir.outputs.fhirId
output TenantId string = tenantIdResolved
output FunctionBaseUrl string = authCustomOperation.outputs.functionAppUrl
output FunctionAppManagedIdentityPrincipalId string = authCustomOperation.outputs.functionAppPrincipalId
output CacheConnectionString string = CacheConnectionString
output BackendServiceKeyVaultName string = backendVault.?outputs.keyVaultName ?? ''
output BackendServiceKeyVaultUri string = backendVault.?outputs.keyVaultUri ?? ''
