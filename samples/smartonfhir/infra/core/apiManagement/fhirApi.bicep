param apiManagementServiceName string
param fhirBaseUrl string
param apimServiceLoggerId string

resource smartApi 'Microsoft.ApiManagement/service/apis@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/smartv1'
  properties: {
    displayName: 'SMART v1'
    apiRevision: 'v1'
    subscriptionRequired: false
    serviceUrl: fhirBaseUrl
    protocols: [
      'https'
    ]
    path: '/smart'
  }

  resource metadataOverrideOperation 'operations' = {
    name: 'metadatOverride'
    properties: {
      displayName: '/metadata'
      method: 'GET'
      urlTemplate: '/metadata'
    }

    resource metadataOverrideOperationPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/metadataOverrideOperationPolicy.xml')
      }
    }
  }

  resource smartWellKnownOperation 'operations' = {
    name: 'smartWellKnown'
    properties: {
      displayName: 'SMART well-known endpoint'
      method: 'GET'
      urlTemplate: '/.well-known/smart-configuration'
    }

    resource smartWellKnownOperationPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/smartWellKnownOperationPolicy.xml')
      }
    }
  }

  resource allOtherRequestsOperationsGet 'operations' = {
    name: 'allOtherRequestsGet'
    properties: {
      displayName: 'all-other-operations GET'
      method: 'GET'
      urlTemplate: '/*'
    }

    resource allOtherRequestsOperationsGetPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('./policies/fhirRequestCheckToken.xml')
      }
    }
  }

  resource allOtherRequestsOperationsPost 'operations' = {
    name: 'allOtherRequestsPost'
    properties: {
      displayName: 'all-other-operations POST'
      method: 'POST'
      urlTemplate: '/*'
    }
  }

  resource smartStyleGet 'operations' = {
    name: 'smartStyleGet'
    properties: {
      displayName: 'SMART Style'
      method: 'GET'
      urlTemplate: '/smart-style.json'
    }

    resource smartStyleGetPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/returnSmartStyleJson.xml')
      }
    }
  }

  resource smartApiDiagnostics 'diagnostics' = {
    name: 'applicationinsights'
    properties: {
      alwaysLog: 'allErrors'
      httpCorrelationProtocol: 'W3C'
      verbosity: 'information'
      logClientIp: true
      loggerId: apimServiceLoggerId
      sampling: {
        samplingType: 'fixed'
        percentage: 100
      }
    }
  }
}
