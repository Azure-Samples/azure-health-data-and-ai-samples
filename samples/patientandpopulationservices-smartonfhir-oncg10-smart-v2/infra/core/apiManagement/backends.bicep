param apiManagementServiceName string
param fhirBaseUrl string
param smartAuthFunctionBaseUrl string
param exportFunctionBaseUrl string
param contextFrontendAppBaseUrl string

resource fhirBackend 'Microsoft.ApiManagement/service/backends@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/fhir'
  properties: {
    url: fhirBaseUrl
    protocol: 'http'
  }
}

resource smartAuthFunctionBackend 'Microsoft.ApiManagement/service/backends@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/smartAuth'
  properties: {
    url: smartAuthFunctionBaseUrl
    protocol: 'http'
  }
}

resource exportFunctionBackend 'Microsoft.ApiManagement/service/backends@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/export'
  properties: {
    url: exportFunctionBaseUrl
    protocol: 'http'
  }
}

resource contextFrontentAppBackend 'Microsoft.ApiManagement/service/backends@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/contextFrontentAppBackend'
  properties: {
    url: contextFrontendAppBaseUrl
    protocol: 'http'
  }
}

output fhirBackendId string = fhirBackend.id
output smartAuthFunctionBackendId string = smartAuthFunctionBackend.id
output exportFunctionBackendId string = exportFunctionBackend.id
output contextFrontendAppBaseUrlId string = contextFrontentAppBackend.id
