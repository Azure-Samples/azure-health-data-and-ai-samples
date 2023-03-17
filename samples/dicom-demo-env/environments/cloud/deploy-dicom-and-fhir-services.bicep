// Bicep file to deploy Azure Health Data Services DICOM and FHIR services
//    Defaults to deploying a single DICOM service
//    Used by other deployments, such as deploying OHIF with a DICOM service

@description('Specifies the location for resources.')
param location string = resourceGroup().location

@description('Name of the AHDS workspace. This will appear in the API URL.')
param workspace_name string = 'workspace${uniqueString(resourceGroup().id)}'

@description('Name of the AHDS DICOM service. This will appear in the API URL.')
param dicom_service_name string = 'dicom${uniqueString(resourceGroup().id)}'

@description('Name of the AHDS FHIR service. This will appear in the API URL.')
param fhir_service_name string = 'fhir${uniqueString(resourceGroup().id)}'

@description('Should deploy FHIR')
param should_deploy_fhir bool = false

@description('Should deploy DICOM')
param should_deploy_dicom bool = true

@description('Should add DICOM App Registration')
param should_add_dicom_app_registration bool = true

// This is VERY confusing! And you can't get the Principal Object ID from the Portal UI. 
//    See https://github.com/Azure/terraform-azurerm-appgw-ingress-k8s-cluster/issues/1 ') for how to get it

// When adding role assignments, and get an error: Principals of type Application cannot validly be used in role as...
// 	that means you're using the Application's Object ID. You need to find the Application's associated Principal Object ID...  
// see: https://github.com/Azure/terraform-azurerm-appgw-ingress-k8s-cluster/issues/1
// and: https://github.com/Azure/azure-cli/issues/5340
// Basically run: az ad sp list --filter "displayName eq 'rsna-demo-viewer-app1'" and get the id provided there - of course with your Application ID name
@description('Object ID of the Principal of the Application - confusing - see https://github.com/Azure/terraform-azurerm-appgw-ingress-k8s-cluster/issues/1 ')
@secure()
param dicom_principalId string  // Object ID of rsna app reg
    

var tenantId = tenant().tenantId
var loginURL = environment().authentication.loginEndpoint
var authority = '${loginURL}${tenantId}'
var audience = 'https://${workspace_name}-${fhir_service_name}.fhir.azurehealthcareapis.com'

// This role is the DICOM Owner Role
var dicom_roleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions','58a3b984-7adf-4c20-983a-32417c86fbc8')

resource workspace 'Microsoft.HealthcareApis/workspaces@2022-06-01' = {
  name: workspace_name
  location: location

  resource dicom 'dicomservices' = if (should_deploy_dicom) {
    name: dicom_service_name
    location: location
  }

  resource fhir 'fhirservices' = if (should_deploy_fhir) {
    name: fhir_service_name
    location: location
    kind: 'fhir-R4'
    identity: {
      type: 'SystemAssigned'
    }
    properties: {
      accessPolicies: []
      authenticationConfiguration: {
        authority: authority
        audience: audience
        smartProxyEnabled: false
      }
    }
  }
}


//// Below doesn't work due to: https://github.com/Azure/terraform-azurerm-appgw-ingress-k8s-cluster/issues/1
// resource myrole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
//   name: guid('58a3b984-7adf-4c20-983a-32417c86fbc8','1fac7ff7-aaf1-4086-91bb-f7c9b98877d5') //just a couple things to get a random guid
//   scope: workspace::dicom //is this how we attach to a particular DICOM server?
//   properties: {
//     //roleDefinitionId: '58a3b984-7adf-4c20-983a-32417c86fbc8' //fixed Guid, from Smitha & portal
//     //roleDefinitionId: '/providers/Microsoft.Authorization/roleDefinitions/58a3b984-7adf-4c20-983a-32417c86fbc8' //fixed, from Smitha
//     roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions','58a3b984-7adf-4c20-983a-32417c86fbc8')
//     principalId: '1fac7ff7-aaf1-4086-91bb-f7c9b98877d5' // Object ID of rsna app reg
//     principalType: 'ServicePrincipal'
//   }
// }

// // The below works great!
// resource myrole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (should_add_dicom_app_registration) {
//   name: guid('58a3b984-7adf-4c20-983a-32417c86fbc8','1fac7ff7-aaf1-4086-91bb-f7c9b98877d5') //just a couple things to get a random guid
//   scope: workspace::dicom //is this how we attach to a particular DICOM server?
//   properties: {
//     roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions','58a3b984-7adf-4c20-983a-32417c86fbc8')
//     principalId: '7e086154-4646-4550-b350-f94fadc6720b' // Object ID of rsna app reg
//     principalType: 'ServicePrincipal'
//   }
// }

resource myrole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (should_add_dicom_app_registration) {
  name: guid(dicom_roleDefinitionId,dicom_principalId,resourceGroup().id) //just a couple things to get a semi-random guid, but same for same inputs
  scope: workspace::dicom //is this how we attach to a particular DICOM server?
  dependsOn: [
    workspace
  ]
  properties: {
    roleDefinitionId: dicom_roleDefinitionId
    principalId: dicom_principalId
    principalType: 'ServicePrincipal'
  }
}


output workspace_name string = workspace_name
output dicom_service_name string = dicom_service_name
output fhir_service_name string = fhir_service_name
output dicom_uri string = workspace::dicom.properties.serviceUrl
output fhir_uri string = audience
output fhir_deployed bool = should_deploy_fhir
output dicom_deployed bool = should_deploy_dicom
output tenantId string = tenantId
output loginURL string = loginURL
output myrolescope string = myrole.properties.scope
output myrolename string = myrole.name
output myroletype string = myrole.type
output myroleid string = myrole.id
output myroleproperties object = myrole.properties


