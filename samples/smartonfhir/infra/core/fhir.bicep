param createWorkspace bool
param createFhirService bool
param workspaceName string
param fhirServiceName string
param tenantId string
param location string
param audience string = ''
param appTags object = {}
param AuthorityURL string
param FhirResourceAppId string
// param smartOnFhirWithB2C bool
param idpProvider string

var loginURL = environment().authentication.loginEndpoint
var authority = '${loginURL}${tenantId}'
var resolvedAudience = idpProvider == 'EntraID' && length(audience) > 0 ? audience :  'https://${workspaceName}-${fhirServiceName}.fhir.azurehealthcareapis.com'
resource healthWorkspace 'Microsoft.HealthcareApis/workspaces@2021-06-01-preview' = if (createWorkspace) {
  name: workspaceName
  location: location
  tags: appTags
}

resource healthWorkspaceExisting 'Microsoft.HealthcareApis/workspaces@2021-06-01-preview' existing = if (!createWorkspace) {
  name: workspaceName
}
var newOrExistingWorkspaceName = createWorkspace ? healthWorkspace.name : healthWorkspaceExisting.name

var authenticationConfiguration = idpProvider != 'EntraID' ? {
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

resource fhir 'Microsoft.HealthcareApis/workspaces/fhirservices@2023-12-01' = if (createFhirService) {
  name: '${newOrExistingWorkspaceName}/${fhirServiceName}'
  location: location
  kind: 'fhir-R4'

  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    authenticationConfiguration: authenticationConfiguration
  }

  tags: appTags
}

resource fhirExisting 'Microsoft.HealthcareApis/workspaces/fhirservices@2023-12-01' existing = if (!createFhirService) {
  name: '${newOrExistingWorkspaceName}/${fhirServiceName}'
}

output fhirId string = createFhirService ? fhir.id : fhirExisting.id
#disable-next-line BCP053
output fhirIdentity string = createFhirService ? fhir.identity.principalId : fhirExisting.identity.principalId
