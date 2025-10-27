param createWorkspace bool
param createFhirService bool
param workspaceName string
param fhirServiceName string
param exportStoreName string
param tenantId string
param location string
param audience string = ''
param appTags object = {}

var loginURL = environment().authentication.loginEndpoint
var authority = '${loginURL}${tenantId}'
var isFhirService = length(workspaceName) > 0
var resolvedAudience = length(audience) > 0 ? audience : isFhirService ? 'https://${workspaceName}-${fhirServiceName}.fhir.azurehealthcareapis.com' : 'https://${fhirServiceName}.azurehealthcareapis.com'

resource healthWorkspace 'Microsoft.HealthcareApis/workspaces@2021-06-01-preview' = if (createWorkspace) {
  name: workspaceName
  location: location
  tags: appTags
}

resource healthWorkspaceExisting 'Microsoft.HealthcareApis/workspaces@2021-06-01-preview' existing = if (isFhirService && !createWorkspace) {
  name: workspaceName
}
var newOrExistingWorkspaceName = createWorkspace ? healthWorkspace.name : isFhirService ? healthWorkspaceExisting.name : ''

resource fhir 'Microsoft.HealthcareApis/workspaces/fhirservices@2021-06-01-preview' = if (createFhirService) {
  name: '${newOrExistingWorkspaceName}/${fhirServiceName}'
  location: location
  kind: 'fhir-R4'

  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    authenticationConfiguration: {
      authority: authority
      audience: resolvedAudience
      smartProxyEnabled: false
    }
     exportConfiguration: {
      storageAccountName: exportStorageAccount.name
    }
  }

  tags: appTags
}

@description('FHIR Export required linked storage account')
resource exportStorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: exportStoreName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  tags: appTags
}

module exportFhirRoleAssignment './identity.bicep'= if(isFhirService) {
  name: 'fhirExportRoleAssignment'
  params: {
    #disable-next-line BCP053
    principalId: createFhirService ? fhir.identity.principalId : fhirExisting.identity.principalId
    fhirId: createFhirService ? fhir.id : fhirExisting.id
    roleType: 'storageBlobContributor'
  }
}

module exportApiForFhirRoleAssignment './identity.bicep'= if (!isFhirService) {
  name: 'apiForFhirExportRoleAssignment'
  params: {
    #disable-next-line BCP053
    principalId: apiForFhirExisting.identity.principalId
    fhirId: apiForFhirExisting.id
    roleType: 'storageBlobContributor'
  }
}

resource fhirExisting 'Microsoft.HealthcareApis/workspaces/fhirservices@2021-06-01-preview' existing = if (isFhirService && !createFhirService) {
  name: '${newOrExistingWorkspaceName}/${fhirServiceName}'
}

resource apiForFhirExisting 'Microsoft.HealthcareApis/services@2025-04-01-preview' existing = if (!isFhirService) {
  name: fhirServiceName
}

output fhirId string = createFhirService ? fhir.id : isFhirService ? fhirExisting.id : apiForFhirExisting.id
#disable-next-line BCP053
output fhirIdentity string = createFhirService ? fhir.identity.principalId : isFhirService ? fhirExisting.identity.principalId : apiForFhirExisting.identity.principalId
output exportStorageUrl string = exportStorageAccount.properties.primaryEndpoints.blob
