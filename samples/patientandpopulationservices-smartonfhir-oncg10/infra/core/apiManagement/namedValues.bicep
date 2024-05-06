param apiManagementServiceName string
param tenantId string
param issuer string
param jwksUri string
param contextStaticAppBaseUrl string
param audienceUrl string
param oncEnabled bool
param SmartOnFhirWithB2C bool

resource tenantIdNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/tenantId'
  properties: {
    displayName: 'TenantId'
    value: tenantId
  }
}

resource issuerNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/issuer'
  properties: {
    displayName: 'Issuer'
    value: issuer
  }
}

resource jwksUriNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/jwksUri'
  properties: {
    displayName: 'JwksUri'
    value: jwksUri
  }
}

resource contextStaticAppNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/contextStaticAppBaseUrl'
  properties: {
    displayName: 'contextStaticAppBaseUrl'
    value: contextStaticAppBaseUrl
  }
}

resource audienceNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/Audience'
  properties: {
    displayName: 'Audience'
    value: '${audienceUrl}/smart'
  }
}

resource oncEnabledValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/oncEnabled'
  properties: {
    displayName: 'oncEnabled'
    value: toLower('${oncEnabled}')
  }
}

resource SmartOnFhirWithB2CValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/SmartOnFhirWithB2C'
  properties: {
    displayName: 'SmartOnFhirWithB2C'
    value: toLower('${SmartOnFhirWithB2C}')
  }
}
