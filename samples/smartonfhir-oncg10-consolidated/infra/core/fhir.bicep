param createWorkspace bool
param workspaceName string
param fhirServiceName string
param exportStoreName string
param tenantId string
param location string
param audience string = ''
param appTags object = {}
param AuthorityURL string
param FhirResourceAppId string
param smartOnFhirWithB2C bool

var loginURL = environment().authentication.loginEndpoint
var authority = '${loginURL}${tenantId}'
var resolvedAudience = !smartOnFhirWithB2C && length(audience) > 0 ? audience :  'https://${workspaceName}-${fhirServiceName}.fhir.azurehealthcareapis.com'

resource healthWorkspace 'Microsoft.HealthcareApis/workspaces@2021-06-01-preview' = if (createWorkspace) {
  name: workspaceName
  location: location
  tags: appTags
}

resource healthWorkspaceExisting 'Microsoft.HealthcareApis/workspaces@2021-06-01-preview' existing = if (!createWorkspace) {
  name: workspaceName
}

var newOrExistingWorkspaceName = createWorkspace ? healthWorkspace.name : healthWorkspaceExisting.name

var authenticationConfiguration = smartOnFhirWithB2C ? {
  authority: authority
  audience: resolvedAudience
  smartProxyEnabled: false
  smartIdentityProviders: [
      {
          authority: AuthorityURL
          applications: [
              {
                  clientId: 'ExternalAppClientId'
                  audience: FhirResourceAppId
                  allowedDataActions: ['Read']
              }
          ]
      }
  ]
} : {
  authority: authority
  audience: resolvedAudience
  smartProxyEnabled: false
}

resource fhir 'Microsoft.HealthcareApis/workspaces/fhirservices@2023-12-01' = {
  name: '${newOrExistingWorkspaceName}/${fhirServiceName}'
  location: location
  kind: 'fhir-R4'

  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    authenticationConfiguration: authenticationConfiguration
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

module exportFhirRoleAssignment './identity.bicep'= {
  name: 'fhirExportRoleAssignment'
  params: {
    #disable-next-line BCP053
    principalId: fhir.identity.principalId
    fhirId: fhir.id
    roleType: 'storageBlobContributor'
  }
}


output fhirId string = fhir.id
#disable-next-line BCP053
output fhirIdentity string = fhir.identity.principalId
output exportStorageUrl string = exportStorageAccount.properties.primaryEndpoints.blob
