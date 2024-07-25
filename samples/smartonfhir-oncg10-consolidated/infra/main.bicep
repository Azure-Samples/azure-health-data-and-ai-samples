targetScope = 'subscription'

// start azd populated parameters

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is a prefix for all resources')
param name string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Id of the user or app to assign application roles. Passed in by Azure Developer CLI.')
param principalId string = ''

// end azd populated parameters

// start required API gateway parameters

@description('Name of the owner of the API Management resource')
@minLength(4)
param ApiPublisherName string

@description('Email of the owner of the API Management resource')
@minLength(8)
param ApiPublisherEmail string

@description('ClientId for the context static app registration for this FHIR Service (you must create this)')
param ContextAppClientId string

@description('Audience for SMART scopes in Microsoft Entra ID. Leave blank to use the PaaS Service URL.')
param FhirAudience string

// end user required API gateway parameters

// start optional configuration parameters

@description('Name of your existing resource group (leave blank to create a new one)')
param existingResourceGroupName string 

@description('Provide the exisiting fhir Service Id(To Get the fhir Id Go to your fhir service -> properties -> Copy the Id under essentials)')
param fhirId string 

@description('Provide authority url to ensure that only authorized users can access sensitive patient information')
param AuthorityURL string

@description('Provide standalone App registration Client Id to access your FHIR Service')
param StandaloneAppClientId string

@description('Provide Fhir Resource App registration Client Id to customize the access token sent to the FHIR Service')
param FhirResourceAppId string

@description('Provide B2C Tenant Id')
param B2CTenantId string

@description('smart on fhir with b2c')
param smartonfhirwithb2c bool 

@description('Is ONC enabled')
param oncEnabled bool

@description('Name of the Log Analytics workspace to deploy or use. Leave blank to skip deployment')
param logAnalyticsName string = ''

@description('Deploy sample with Virtual Network')
param enableVNetSupport bool

// end optional configuration parameters

var nameClean = replace(name, '-', '')
var nameCleanShort = length(nameClean) > 16 ? substring(nameClean, 0, 16) : nameClean
var nameShort = length(name) > 16 ? substring(name, 0, 16) : name
var fhirResourceIdSplit = split(fhirId,'/')
var fhirserviceRg = empty(fhirId) ? '' : fhirResourceIdSplit[4]
var createWorkspace = empty(fhirId) ? true : false

var appTags = {
  AppID: 'fhir-smart-onc-g10-sample'
  'azd-env-name': name
}

var tenantId = subscription().tenantId

// Add any extra principals that need to be able to access the Key Vault
var keyVaultWriterPrincipals = [ principalId ]
var fhirSMARTPrincipals = []
var fhirContributorPrincipals = [ principalId ]
var createResourceGroup = empty(existingResourceGroupName) ? true : false


@description('Resource group to deploy sample in.')
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = if (createResourceGroup) {
  name: '${name}-rg'
  location: location
  tags: appTags
}

var workspaceNameResolved = empty(fhirId) ? '${replace(nameCleanShort, '-', '')}health' : fhirResourceIdSplit[8]
var fhirNameResolved = empty(fhirId) ? 'fhirdata' : fhirResourceIdSplit[10]
var fhirUrl = 'https://${workspaceNameResolved}-${fhirNameResolved}.fhir.azurehealthcareapis.com'


resource existingResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' existing = if (!createResourceGroup) {
  name: existingResourceGroupName
}

var AuthorityURLvalue = empty(AuthorityURL) ? '' : AuthorityURL
var StandaloneAppClientIdvalue = empty(StandaloneAppClientId) ? '': StandaloneAppClientId
var FhirResourceAppIdvalue = empty(StandaloneAppClientId)? '': FhirResourceAppId
var newOrExistingResourceGroupName = createResourceGroup ? rg.name : existingResourceGroup.name
var fhirInstanceResourceGroup = empty(fhirId) ? newOrExistingResourceGroupName : fhirserviceRg

@description('Deploy Azure Health Data Services and FHIR service')
module fhir 'core/fhir.bicep'= {
  name: 'azure-health-data-services'
  scope: resourceGroup(fhirInstanceResourceGroup)
  params: {
    createWorkspace: createWorkspace
    workspaceName: workspaceNameResolved
    fhirServiceName: fhirNameResolved
    exportStoreName: functionBase.outputs.storageAccountName
    location: location
    tenantId: tenantId
    appTags: appTags
    audience: FhirAudience
    AuthorityURL: AuthorityURLvalue
    StandaloneAppClientId:StandaloneAppClientIdvalue
    FhirResourceAppId:FhirResourceAppIdvalue
    smartOnFhirWithB2C: smartonfhirwithb2c
  }
}

@description('Name for app insights resource used to monitor the Function App')
var appInsightsName = '${nameCleanShort}-appins'

var logAnalyticsNameResolved = length(logAnalyticsName) > 0 ? logAnalyticsName : '${nameCleanShort}-la'

@description('Deploy monitoring and logging')
module monitoring 'core/monitoring.bicep'= {
  name: 'monitoringDeploy'
  scope: resourceGroup(newOrExistingResourceGroupName)
  params: {
    logAnalyticsName: logAnalyticsNameResolved
    appInsightsName: appInsightsName
    location: location
    appTags: appTags
  }
}

@description('Deploy base resources needed for function app based custoom operations.')
module functionBase 'core/functionHost.bicep' = {
  name: 'functionBaseDeploy'
  scope: resourceGroup(newOrExistingResourceGroupName)
  params: {
    appTags: appTags
    location: location
    name: name
    nameCleanShort: nameCleanShort
    enableVNetSupport: enableVNetSupport
  }
}

@description('Deploy Redis Cache for use as External Cache for APIM')
module redis './core/redisCache.bicep'= {
  name: 'redisCacheDeploy'
  scope: resourceGroup(newOrExistingResourceGroupName)
  params: {
    apiManagementServiceName: apimName
    location: location
  }
}

@description('Azure Health Data Services Toolkit auth custom operation function app')
module authCustomOperation './app/authCustomOperation.bicep' = {
  name: 'authCustomOperationDeploy'
  scope: resourceGroup(newOrExistingResourceGroupName)
  params: {
    name: name
    location: location
    appTags: appTags
    tenantId: tenantId
    apimName: apimName
    smartFrontendAppUrl: contextStaticWebApp.outputs.uri
    fhirServiceAudience: FhirAudience
    backendServiceVaultName: backendServiceVaultName
    contextAadApplicationId: ContextAppClientId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    appInsightsInstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
    customOperationsFuncStorName: functionBase.outputs.storageAccountName
    hostingPlanId: functionBase.outputs.hostingPlanId
    redisCacheId: redis.outputs.redisCacheId
    redisApiVersion: redis.outputs.redisApiVersion
    redisCacheHostName: redis.outputs.redisCacheHostName
     b2cTenantId: B2CTenantId
    fhirResourceAppId: FhirResourceAppId
    authorityUrl: AuthorityURL
    smartOnFhirWithB2C: smartonfhirwithb2c
    standaloneAppClientId: StandaloneAppClientId
    enableVNetSupport: enableVNetSupport
  }
}

@description('Azure Health Data Services Toolkit export custom operation function app')
module exportCustomOperation './app/exportCustomOperation.bicep' = {
  name: 'exportCustomOperationDeploy'
  scope: resourceGroup(newOrExistingResourceGroupName)
  params: {
    name: name
    location: location
    appTags: appTags
    tenantId: tenantId
    apimName: apimName
    fhirUrl: fhirUrl
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    appInsightsInstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
    customOperationsFuncStorName: functionBase.outputs.storageAccountName
    hostingPlanId: functionBase.outputs.hostingPlanId
    enableVNetSupport: enableVNetSupport
  }
}

@description('Setup identity connection between FHIR and the given contributors')
module fhirContributorIdentities './core/identity.bicep' =  [for principalId in  fhirContributorPrincipals: {
  name: 'fhirIdentity-${principalId}-fhirContrib'
  scope: resourceGroup(fhirInstanceResourceGroup)
  params: {
    fhirId: fhir.outputs.fhirId
    principalId: principalId
    principalType: 'User'
    roleType: 'fhirContributor'
  }
}]

@description('Setup identity connection between FHIR and the given SMART users')
module fhirSMARTIdentities './core/identity.bicep' =  [for principalId in  fhirSMARTPrincipals: {
  name: 'fhirIdentity-${principalId}-fhirSmart'
  scope: resourceGroup(fhirInstanceResourceGroup)
  params: {
    fhirId: fhir.outputs.fhirId
    principalId: principalId
    principalType: 'User'
    roleType: 'fhirSmart'
  }
}]

@description('Setup identity connection between Export functon app and export storage account')
module exportFhirRoleAssignment './core/identity.bicep'= {
  name: 'fhirExportRoleAssignment'
  scope: resourceGroup(newOrExistingResourceGroupName)
  params: {
    principalId: exportCustomOperation.outputs.functionAppPrincipalId
    fhirId: fhir.outputs.fhirId
    roleType: 'storageBlobContributor'
  }
}

var apimName = '${name}-apim'

@description('Deploy Azure API Management for the FHIR gateway')
module apim './core/apiManagement.bicep'= {
  name: 'apiManagementDeploy'
  scope: resourceGroup(newOrExistingResourceGroupName)
  params: {
    apiManagementServiceName: apimName
    publisherEmail: ApiPublisherEmail
    publisherName: ApiPublisherName
    location: location
    fhirBaseUrl: fhirUrl
    issuer: issuer
    jwksUri: jwksUri
    oncEnabled: oncEnabled
    smartAuthFunctionBaseUrl: 'https://${name}-aad-func.azurewebsites.net/api'
    exportFunctionBaseUrl: 'https://${name}-exp-func.azurewebsites.net/api'
    contextStaticAppBaseUrl: contextStaticWebApp.outputs.uri
    appInsightsInstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
    enableVNetSupport: enableVNetSupport
  }
}

@description('Link Redis Cache to APIM')
module redisApimLink './core/apiManagement/redisExternalCache.bicep'= {
  name: 'apimRedisLinkDeploy'
  scope: resourceGroup(newOrExistingResourceGroupName)
  params: {
    apiManagementServiceName: apimName
    redisApiVersion: redis.outputs.redisApiVersion
    redisCacheHostName: redis.outputs.redisCacheHostName
    redisCacheId: redis.outputs.redisCacheId
  }
  dependsOn:[
    apim
  ]
}

var backendServiceVaultName = '${nameShort}-backkv'
@description('KeyVault to hold backend service principal maps')
module keyVault './core/keyVault.bicep' = {
  name: 'vaultDeploy'
  scope: resourceGroup(newOrExistingResourceGroupName)
  params: {
    vaultName: backendServiceVaultName
    location: location
    tenantId: tenantId
    writerObjectIds: keyVaultWriterPrincipals
    readerObjectIds: [ authCustomOperation.outputs.functionAppPrincipalId ]
  }
}

var authorizeStaticWebAppName = '${name}-contextswa'
@description('Static web app for SMART Context UI')
module contextStaticWebApp './app/contextApp.bicep' = {
  name: 'staticWebAppDeploy'
  scope: resourceGroup(newOrExistingResourceGroupName)
  params: {
    staticWebAppName: authorizeStaticWebAppName
    location: location
    appTags: union(appTags, {
      'azd-service-name': 'context'
    })
    enableVNetSupport: enableVNetSupport
  }
}

var tenantnamesplit = split(AuthorityURL,'/')  
var tenantendpoint = smartonfhirwithb2c ? tenantnamesplit[2] : ''
var b2ctenantname = smartonfhirwithb2c ? split(tenantendpoint,'.') : ['']

var issuer = smartonfhirwithb2c ? 'https://${tenantendpoint}/${B2CTenantId}/v2.0/' : 'https://${tenantendpoint}/${tenantId}/v2.0'
var jwksUri = endsWith(AuthorityURL, '/v2.0') ? substring(AuthorityURL, 0, length(AuthorityURL) - 5) : AuthorityURL

// These map to user secrets for local execution of the program
output Location string = location
output TenantId string = tenantId
output FhirUrl string = fhirUrl
output FhirAudience string = authCustomOperation.outputs.authCustomOperationAudience
output ExportStorageAccountUrl string = 'https://${functionBase.outputs.storageAccountName}.blob.${environment().suffixes.storage}'
output ApiManagementHostName string = apim.outputs.apimHostName
output BackendServiceKeyVaultStore string = backendServiceVaultName
output ContextAppClientId string = ContextAppClientId
output CacheConnectionString string = authCustomOperation.outputs.cacheConnectionString
output AzureAuthCustomOperationManagedIdentityId string = authCustomOperation.outputs.functionAppPrincipalId
output REACT_APP_AAD_APP_CLIENT_ID string = ContextAppClientId
output REACT_APP_AAD_APP_TENANT_ID string = tenantId
output REACT_APP_API_BASE_URL string = 'https://${apim.outputs.apimHostName}'
output REACT_APP_FHIR_RESOURCE_AUDIENCE string = FhirAudience
output B2C_Tenant_Name string = b2ctenantname[0]
output Authority_URL string = AuthorityURL
output AZURE_RESOURCE_GROUP string = newOrExistingResourceGroupName
output SmartonFhir_with_B2C bool = smartonfhirwithb2c
output B2C_Tenant_Id string = B2CTenantId
output Standalone_App_ClientId string = StandaloneAppClientId
output Fhir_Resource_AppId string = FhirResourceAppId

output REACT_APP_B2C_Tenant_Name string= b2ctenantname[0]
output REACT_APP_SmartonFhir_with_B2C bool = smartonfhirwithb2c
output REACT_APP_Authority_URL string = endsWith(AuthorityURL, '/v2.0') ? substring(AuthorityURL, 0, length(AuthorityURL) - 5) : AuthorityURL
