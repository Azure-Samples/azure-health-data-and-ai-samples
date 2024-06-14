param createWorkspace bool
param workspaceName string
param fhirServiceName string
param tenantId string
param location string
param audience string = ''
param appTags object = {}
param AuthorityURL string
param StandaloneAppClientId string
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
                  clientId: StandaloneAppClientId
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
  }

  tags: appTags
}

output fhirId string = fhir.id
#disable-next-line BCP053
output fhirIdentity string = fhir.identity.principalId
