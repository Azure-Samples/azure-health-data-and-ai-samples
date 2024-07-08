@description('The name of the API Management service instance')
param apiManagementServiceName string

@description('Location for API Management service instance.')
param location string = resourceGroup().location
 
param enableVNetSupport bool

@description('The pricing tier of this API Management service')
@allowed(['Consumption', 'Basic', 'Developer', 'Standard', 'Premium'])
param sku string = enableVNetSupport ? 'Premium' : 'Consumption'

@description('The instance size of this API Management service.')
@allowed([0, 1, 2])
param skuCount int = enableVNetSupport ? 1 : 0

@description('The name of the owner of the service')
@minLength(1)
param publisherName string

@description('The email address of the owner of the service')
@minLength(1)
param publisherEmail string

@description('Base URL of the FHIR Service')
param fhirBaseUrl string

@description('Base URL of the SMART Auth Custom Operation Function')
param smartAuthFunctionBaseUrl string

@description('Base URL of the static webapp with Authorize Context Handles')
param contextStaticAppBaseUrl string


@description('Instrumentation key for App Insights used with APIM')
param appInsightsInstrumentationKey string

@description('Core API Management Service Resources')
module apimService 'apiManagement/service.bicep' = {
  name: '${apiManagementServiceName}-service'
  params: {
    apiManagementServiceName: apiManagementServiceName
    location: location
    sku: sku
    skuCount: skuCount
    publisherName: publisherName
    publisherEmail: publisherEmail
    appInsightsInstrumentationKey: appInsightsInstrumentationKey
  }
}

@description('API Management Backends')
module apimBackends 'apiManagement/backends.bicep' = {
  name: '${apiManagementServiceName}-backends'
  params: {
    apiManagementServiceName: apiManagementServiceName
    fhirBaseUrl: fhirBaseUrl
    smartAuthFunctionBaseUrl: smartAuthFunctionBaseUrl
    contextFrontendAppBaseUrl: contextStaticAppBaseUrl
  }

  dependsOn: [ apimService ]
}

@description('Configuration for FHIR API')
module apimFhirApi 'apiManagement/fhirApi.bicep' = {
  name: '${apiManagementServiceName}-api-fhir'
  params: {
    apiManagementServiceName: apiManagementServiceName
    fhirBaseUrl: fhirBaseUrl
    apimServiceLoggerId: apimService.outputs.serviceLoggerId
  }

  dependsOn: [ apimBackends ]
}

@description('Configuration for SMART on FHIR Auth Custom Operations API')
module apimSmartAuthApi 'apiManagement/smartAuthApi.bicep' = {
  name: '${apiManagementServiceName}-api-smart-auth'
  params: {
    apiManagementServiceName: apiManagementServiceName
    apimServiceLoggerId: apimService.outputs.serviceLoggerId
    authCustomOperationsBaseUrl: smartAuthFunctionBaseUrl
  }

  dependsOn: [ apimBackends ]
}

@description('API Management Named Values (configuration)')
module apimNamedValues 'apiManagement/namedValues.bicep' = {
  name: '${apiManagementServiceName}-named-values'
  params: {
    apiManagementServiceName: apiManagementServiceName
    tenantId: subscription().tenantId
    contextStaticAppBaseUrl: contextStaticAppBaseUrl
    audienceUrl: 'https://${apiManagementServiceName}.azure-api.net'
  }

  dependsOn: [ apimService ]
}

@description('API Management Policy Fragments')
module apimFragments 'apiManagement/fragments.bicep' = {
  name: '${apiManagementServiceName}-fragments'
  params: {
    apiManagementServiceName: apiManagementServiceName
  }

  dependsOn: [ apimService ]
}

output apimHostName string = '${apiManagementServiceName}.azure-api.net'
output apimUrl string = 'https://${apiManagementServiceName}.azure-api.net'
