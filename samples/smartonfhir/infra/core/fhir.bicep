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
param smartOnFhirWithB2C bool

var loginURL = environment().authentication.loginEndpoint
var authority = '${loginURL}${tenantId}'
var isFhirService = length(workspaceName) > 0
var resolvedAudience = !smartOnFhirWithB2C && length(audience) > 0 ? audience : isFhirService ? 'https://${workspaceName}-${fhirServiceName}.fhir.azurehealthcareapis.com' : 'https://${fhirServiceName}.azurehealthcareapis.com'

resource healthWorkspace 'Microsoft.HealthcareApis/workspaces@2021-06-01-preview' = if (createWorkspace) {
  name: workspaceName
  location: location
  tags: appTags
}

resource healthWorkspaceExisting 'Microsoft.HealthcareApis/workspaces@2021-06-01-preview' existing = if (isFhirService && !createWorkspace) {
  name: workspaceName
}
var newOrExistingWorkspaceName = createWorkspace ? healthWorkspace.name : isFhirService ? healthWorkspaceExisting.name : ''

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

resource fhir 'Microsoft.HealthcareApis/workspaces/fhirservices@2023-12-01' = if(createFhirService) {
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

resource fhirExisting 'Microsoft.HealthcareApis/workspaces/fhirservices@2023-12-01' existing = if (isFhirService && !createFhirService) {
  name: '${newOrExistingWorkspaceName}/${fhirServiceName}'
}

resource apiForFhirExisting 'Microsoft.HealthcareApis/services@2025-04-01-preview' existing = if (!isFhirService && !createFhirService) {
  name: fhirServiceName
}

output fhirId string = createFhirService ? fhir.id : isFhirService ? fhirExisting.id : apiForFhirExisting.id
#disable-next-line BCP053
output fhirIdentity string = createFhirService ? fhir.identity.principalId : isFhirService ? fhirExisting.identity.principalId : apiForFhirExisting.identity.principalId
