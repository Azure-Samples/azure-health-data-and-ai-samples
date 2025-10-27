param apiManagementServiceName string
param authCustomOperationsBaseUrl string
param apimServiceLoggerId string

resource smartAuthApi 'Microsoft.ApiManagement/service/apis@2021-12-01-preview' = {
  name: '${apiManagementServiceName}/auth'
  properties: {
    displayName: 'SMART Auth API'
    apiRevision: 'v1'
    subscriptionRequired: false
    serviceUrl: authCustomOperationsBaseUrl
    protocols: [
      'https'
    ]
    path: '/auth'
  }

  resource smartAppConsentInfoOptions 'operations' = {
    name: 'smartAppConsentInfoEndpointOptions'
    properties: {
      displayName: 'SMART Consent Info (OPTIONS)'
      method: 'OPTIONS'
      urlTemplate: '/appConsentInfo'
    }

    resource smartAuthorizeEndpointPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/appConsentInfoEndpointPolicy.xml')
      }
    }
  }

  resource smartAppConsentInfoGet 'operations' = {
    name: 'smartAppConsentInfoEndpointGet'
    properties: {
      displayName: 'SMART Consent Info (GET)'
      method: 'GET'
      urlTemplate: '/appConsentInfo'
    }

    resource smartAuthorizeEndpointPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/appConsentInfoEndpointPolicy.xml')
      }
    }
  }

  resource smartContextCacheOptions 'operations' = {
    name: 'smartContextCacheOptions'
    properties: {
      displayName: 'SMART Context Cache (OPTIONS)'
      method: 'OPTIONS'
      urlTemplate: '/context-cache'
    }

    resource smartContextCacheOptionsPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/contextCachePolicy.xml')
      }
    }
  }

  resource smartContextCachePost 'operations' = {
    name: 'smartContextCachePost'
    properties: {
      displayName: 'SMART Context Cache (POST)'
      method: 'POST'
      urlTemplate: '/context-cache'
    }

    resource smartContextCachePostPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/contextCachePolicy.xml')
      }
    }
  }

  resource smartContextFrontendAppGet 'operations' = {
    name: 'smartContextFrontendAppGet'
    properties: {
      displayName: 'Auth Context Frontend App (GET)'
      method: 'GET'
      urlTemplate: '/context/*'
    }

    resource smartContextFrontendAppGetPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/authContextRedirectPolicy.xml')
      }
    }
  }

  resource smartAppConsentInfoPost 'operations' = {
    name: 'smartAppConsentInfoEndpointPost'
    properties: {
      displayName: 'SMART Consent Info (POST)'
      method: 'POST'
      urlTemplate: '/appConsentInfo'
    }

    resource smartAuthorizeEndpointPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/appConsentInfoEndpointPolicy.xml')
      }
    }
  }

  resource smartAuthorizeEndpointGet 'operations' = {
    name: 'smartAuthorizeEndpointGet'
    properties: {
      displayName: 'SMART Authorize Endpoint (GET)'
      method: 'GET'
      urlTemplate: '/authorize'
    }

    resource smartAuthorizeEndpointGetPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/authorizeEndpointGetPolicy.xml')
      }
    }
  }

  resource smartAuthorizeEndpointPost 'operations' = {
    name: 'smartAuthorizeEndpointPost'
    properties: {
      displayName: 'SMART Authorize Endpoint (POST)'
      method: 'POST'
      urlTemplate: '/authorize'
    }

    resource smartAuthorizeEndpointPostPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/authorizeEndpointPostPolicy.xml')
      }
    }
  }

  resource smartTokenEndpoint 'operations' = {
    name: 'smartTokenEndpoint'
    properties: {
      displayName: 'SMART Token Endpoint'
      method: 'POST'
      urlTemplate: '/token'
    }

    resource smartAuthorizeEndpointPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/tokenEndpointPolicy.xml')
      }
    }
  }

  resource blockAccessTokenPost 'operations' = {
    name: 'blockAccessTokenPost'
    properties: {
      displayName: 'Block Access Token'
      method: 'POST'
      urlTemplate: '/block-access-token'
    }

    resource blockAccessTokenPostPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/cacheBlockToken.xml')
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

  resource smartTokenIntrospectionEndpoint 'operations' = {
    name: 'smartTokenIntrospectionEndpoint'
    properties: {
      displayName: 'SMART Token Introspection Endpoint'
      method: 'POST'
      urlTemplate: '/token/introspection'
    }

    resource smartAuthorizeEndpointPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: loadTextContent('policies/tokenIntrospectionEndpointPolicy.xml')
      }
    }
  }
}
