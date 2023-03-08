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
param apimPublisherName string = 'Test Name'

@description('Email of the owner of the API Management resource')
param apimPublisherEmail string = 'test@test.com'

@description('ClientId for the context static app registration for this FHIR Service (you must create this)')
param contextAadApplicationId string = '04b07795-8ddb-461a-bbee-02f9e1bf7b46'

// end user required API gateway parameters

// start optional configuration parameters

@description('Do you want to create a new Azure Health Data Services workspace or use an existing one?')
param createWorkspace bool = true

@description('Do you want to create a new FHIR Service or use an existing one?')
param createFhirService bool = true

@description('Name of Azure Health Data Services workspace to deploy or use. Leave blank for default.')
param workspaceName string = ''

@description('Name of the FHIR service to deloy or use. Leave blank for default.')
param fhirServiceName string = ''

@description('Name of the FHIR service to deloy or use. Leave blank for default.')
param exportStoreName string = ''

@description('Audience for SMART scopes in Azure Active Directory. Leave blank to use the PaaS Service URL.')
param smartAudience string = ''

@description('Name of the Log Analytics workspace to deploy or use. Leave blank to skip deployment')
param logAnalyticsName string = ''

// end optional configuration parameters

var nameClean = replace(name, '-', '')
var nameCleanShort = length(nameClean) > 16 ? substring(nameClean, 0, 16) : nameClean

var appTags = {
  AppID: 'fhir-smart-onc-g10-sample'
  'azd-env-name': name
}

var tenantId = subscription().tenantId

// Add any extra principals that need to be able to access the Key Vault
var keyVaultWriterPrincipals = [ principalId ]
var fhirSMARTPrincipals = []
var fhirContributorPrincipals = [ principalId ]


@description('Resource group to deploy sample in.')
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: '${name}-rg'
  location: location
  tags: appTags
}

var workspaceNameResolved = length(workspaceName) > 0 ? workspaceName : '${replace(nameCleanShort, '-', '')}health'
var fhirNameResolved = length(fhirServiceName) > 0 ? workspaceName : 'fhirdata'
var fhirUrl = 'https://${workspaceNameResolved}-${fhirNameResolved}.fhir.azurehealthcareapis.com'
var exportStoreNameResolved = length(exportStoreName) > 0 ? exportStoreName : '${nameCleanShort}exsa'

@description('Deploy Azure Health Data Services and FHIR service')
module fhir 'core/fhir.bicep'= {
  name: 'azure-health-data-services'
  scope: rg
  params: {
    createWorkspace: createWorkspace
    createFhirService: createFhirService
    workspaceName: workspaceNameResolved
    fhirServiceName: fhirNameResolved
    exportStoreName: exportStoreNameResolved
    location: location
    tenantId: tenantId
    appTags: appTags
  }
}

@description('Name for app insights resource used to monitor the Function App')
var appInsightsName = '${nameCleanShort}-appins'

var logAnalyticsNameResolved = length(logAnalyticsName) > 0 ? logAnalyticsName : '${nameCleanShort}-la'

@description('Deploy monitoring and logging')
module monitoring 'core/monitoring.bicep'= {
  name: 'monitoringDeploy'
  scope: rg
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
  scope: rg
  params: {
    appTags: appTags
    location: location
    name: name
    nameClean: nameClean
  }
}

@description('Azure Health Data Services Toolkit auth custom operation function app')
module authCustomOperation './app/authCustomOperation.bicep' = {
  name: 'authCustomOperationDeploy'
  scope: rg
  params: {
    name: name
    location: location
    appTags: appTags
    tenantId: tenantId
    apimName: apimName
    fhirUrl: fhirUrl
    smartFrontendAppUrl: contextStaticWebApp.outputs.uri
    smartAudience: smartAudience
    backendServiceVaultName: backendServiceVaultName
    contextAadApplicationId: contextAadApplicationId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    appInsightsInstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
    customOperationsFuncStorName: functionBase.outputs.storageAccountName
    hostingPlanId: functionBase.outputs.hostingPlanId
  }
}

@description('Azure Health Data Services Toolkit export custom operation function app')
module exportCustomOperation './app/exportCustomOperation.bicep' = {
  name: 'exportCustomOperationDeploy'
  scope: rg
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
  }
}

// #TODO - remove once SMART Scopes are properly working
@description('Setup identity connection between FHIR and the function app')
module functionFhirIdentity './core/identity.bicep'= {
  name: 'fhirIdentity-function-contributor'
  scope: rg
  params: {
    fhirId: fhir.outputs.fhirId
    principalId: authCustomOperation.outputs.functionAppPrincipalId
    roleType: 'fhirContributor'
  }
}

@description('Setup identity connection between FHIR and the given contributors')
module fhirContributorIdentities './core/identity.bicep' =  [for principalId in  fhirContributorPrincipals: {
  name: 'fhirIdentity-${principalId}-fhirContrib'
  scope: rg
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
  scope: rg
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
  scope: rg
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
  scope: rg
  params: {
    apiManagementServiceName: apimName
    publisherEmail: apimPublisherEmail
    publisherName: apimPublisherName
    location: location
    fhirBaseUrl: fhirUrl
    smartAuthFunctionBaseUrl: authCustomOperation.outputs.functionAppUrl
    exportFunctionBaseUrl: exportCustomOperation.outputs.functionAppUrl
    contextStaticAppBaseUrl: contextStaticWebApp.outputs.uri
    appInsightsInstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
  }
}

var backendServiceVaultName = '${name}-backkv'
@description('KeyVault to hold backend service principal maps')
module keyVault './core/keyVault.bicep' = {
  name: 'vaultDeploy'
  scope: rg
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
  scope: rg
  params: {
    staticWebAppName: authorizeStaticWebAppName
    location: location
    appTags: union(appTags, {
      'azd-service-name': 'context'
    })
  }
}

// These map to user secrets for local execution of the program
output LOCATION string = location
output FhirServerUrl string = fhirUrl
// output ExportStorageAccountUrl string = template.outputs.ExportStorageAccountUrl
// output ApiManagementHostName string = template.outputs.ApiManagementHostName
// output BackendServiceKeyVaultStore string = template.outputs.BackendServiceKeyVaultStore
output Audience string = authCustomOperation.outputs.authCustomOperationAudience
output TenantId string = tenantId

output REACT_APP_AAD_APP_CLIENT_ID string = contextAadApplicationId
output REACT_APP_AAD_APP_TENANT_ID string = tenantId
output REACT_APP_API_BASE_URL string = 'https://${apim.outputs.apimHostName}/smart'
