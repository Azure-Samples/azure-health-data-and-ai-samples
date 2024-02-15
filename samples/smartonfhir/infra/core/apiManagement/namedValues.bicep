param apiManagementServiceName string
param tenantId string
param smartOnFhirWithB2C bool
param b2cTenantId string
param b2cTenantEndPoint string
param b2cAuthorityUrl string
param contextStaticAppBaseUrl string
param audienceUrl string

resource tenantIdNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/tenantId'
  properties: {
    displayName: 'TenantId'
    value: tenantId
  }
}

resource smartOnFhirWithB2CNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/smartOnFhirWithB2C'
  properties: {
    displayName: 'SmartOnFhirWithB2C'
    value: '${smartOnFhirWithB2C}'
  }
}

resource b2cTenantIdNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/b2cTenantId'
  properties: {
    displayName: 'B2CTenantId'
    value: b2cTenantId
  }
}

resource b2cTenantEndPointNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/b2cTenantEndPoint'
  properties: {
    displayName: 'B2CTenantEndPoint'
    value: b2cTenantEndPoint
  }
}

resource b2cAuthorityUrlNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/b2cAuthorityUrl'
  properties: {
    displayName: 'B2CAuthorityUrl'
    value: b2cAuthorityUrl
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
