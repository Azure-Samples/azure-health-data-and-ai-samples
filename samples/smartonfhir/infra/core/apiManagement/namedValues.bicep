param apiManagementServiceName string
param tenantId string
param issuer string
param jwksUri string
param contextStaticAppBaseUrl string
param audienceUrl string

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

resource audienceNamedVAlue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/Audience'
  properties: {
    displayName: 'Audience'
    value: '${audienceUrl}/smart'
  }
}
