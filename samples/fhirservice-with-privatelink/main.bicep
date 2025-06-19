@description('Prefix for all resources')
param prefix string

@description('Location for all resources.')
param location string = resourceGroup().location

@metadata({ decription: 'Name of the virtual network to use.' })
param virtualNetworkName string

@metadata({ decription: 'Resource group where the virtual network is located.' })
param virtualNetworkResourceGroup string

@metadata({ decription: 'Name of the subnet to use.' })
param subnetName string

var deploymentPrefix = substring(uniqueString(prefix, resourceGroup().id), 0, 6)
var workspaceName = '${deploymentPrefix}wkspc'
var fhirservicename = '${deploymentPrefix}fhirserver'
var privateEndpointName = '${deploymentPrefix}-privateEndpoint'
var networkInterfacename_var = '${deploymentPrefix}nic'
var privateDnsZonePrivatelinkNameFhir_var = 'privatelink.azurehealthcareapis.com'
var privateDnsZonePrivatelinkNameDicom_var = 'privatelink.dicom.azurehealthcareapis.com'
var tenantId = subscription().tenantId

resource workspace 'Microsoft.HealthcareApis/workspaces@2024-03-31' = {
  name: workspaceName
  location: location
  tags: {
    Use: 'Fhir Service on Virtual network with private link'
  }
  properties: {
    publicNetworkAccess: 'Disabled'
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: privateEndpointName
  location: location
  properties: {
    privateLinkServiceConnections: [
      {
        name: privateEndpointName
        properties: {
          privateLinkServiceId: workspace.id
          groupIds: [
            'healthcareworkspace'
          ]
          privateLinkServiceConnectionState: {
            status: 'Approved'
            description: 'Auto-Approved'
            actionsRequired: 'None'
          }
        }
      }
    ]
    manualPrivateLinkServiceConnections: []
    customNetworkInterfaceName: '${privateEndpointName}-nic'
    subnet: {
      id: resourceId(
        virtualNetworkResourceGroup,
        'Microsoft.Network/virtualNetworks/subnets',
        virtualNetworkName,
        subnetName
      )
    }
    ipConfigurations: []
    customDnsConfigs: []
  }
  dependsOn: [
    workspaceName_fhirservicename
  ]
}

resource privateDnsZonePrivatelinkNameFhir 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZonePrivatelinkNameFhir_var
  location: 'global'
}

resource privateDnsZonePrivatelinkNameDicom 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZonePrivatelinkNameDicom_var
  location: 'global'
  properties: {}
}

resource privateDnsZonePrivatelinkNameFhir_deploymentPrefix 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: privateDnsZonePrivatelinkNameFhir
  name: '${deploymentPrefix}'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: resourceId(virtualNetworkResourceGroup, 'Microsoft.Network/virtualNetworks', virtualNetworkName)
    }
  }
}

resource privateDnsZonePrivatelinkNameDicom_deploymentPrefix 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: privateDnsZonePrivatelinkNameDicom
  name: '${deploymentPrefix}'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: resourceId(virtualNetworkResourceGroup, 'Microsoft.Network/virtualNetworks', virtualNetworkName)
    }
  }
}

resource privateEndpointName_default 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  name: '${privateEndpointName}/default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink-azurehealthcareapis-com'
        properties: {
          privateDnsZoneId: privateDnsZonePrivatelinkNameFhir.id
        }
      }
      {
        name: 'privatelink-dicom-azurehealthcareapis-com'
        properties: {
          privateDnsZoneId: privateDnsZonePrivatelinkNameDicom.id
        }
      }
    ]
  }
  dependsOn: [
    privateEndpoint
  ]
}

resource networkInterfacename 'Microsoft.Network/networkInterfaces@2024-01-01' = {
  name: networkInterfacename_var
  location: location
  kind: 'Regular'
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        type: 'Microsoft.Network/networkInterfaces/ipConfigurations'
        properties: {
          privateIPAddress: '10.0.0.6'
          privateIPAllocationMethod: 'Dynamic'
          subnet: {
            id: resourceId(
              virtualNetworkResourceGroup,
              'Microsoft.Network/virtualNetworks/subnets',
              virtualNetworkName,
              subnetName
            )
          }
          primary: true
          privateIPAddressVersion: 'IPv4'
        }
      }
    ]
    dnsSettings: {
      dnsServers: []
    }
    enableAcceleratedNetworking: true
    enableIPForwarding: false
    disableTcpStateTracking: false
    nicType: 'Standard'
    auxiliaryMode: 'None'
    auxiliarySku: 'None'
  }
  dependsOn: []
}

resource workspaceName_fhirservicename 'Microsoft.HealthcareApis/workspaces/fhirservices@2024-03-31' = {
  parent: workspace
  name: '${fhirservicename}'
  location: location
  kind: 'fhir-R4'
  identity: {
    type: 'None'
  }
  properties: {
    acrConfiguration: {
      loginServers: []
    }
    authenticationConfiguration: {
      authority: 'https://login.microsoftonline.com/${tenantId}'
      audience: 'https://${workspaceName}-${fhirservicename}.fhir.azurehealthcareapis.com'
      smartProxyEnabled: false
      smartIdentityProviders: []
    }
    corsConfiguration: {
      origins: []
      headers: []
      methods: []
      allowCredentials: false
    }
    exportConfiguration: {}
    importConfiguration: {
      enabled: false
      initialImportMode: false
    }
    resourceVersionPolicyConfiguration: {
      default: 'versioned'
      resourceTypeOverrides: {}
    }
    implementationGuidesConfiguration: {
      usCoreMissingData: false
    }
    encryption: {
      customerManagedKeyEncryption: {}
    }
    publicNetworkAccess: 'Disabled'
  }
}

resource privateDnsZonePrivatelinkNameFhir_workspaceName_fhirserv_fhir 'Microsoft.Network/privateDnsZones/A@2020-06-01' = {
  parent: privateDnsZonePrivatelinkNameFhir
  name: '${workspaceName}-fhirserv.fhir'
  properties: {
    metadata: {
      creator: 'created by private endpoint'
    }
    ttl: 10
    aRecords: [
      {
        ipv4Address: '10.0.0.5'
      }
    ]
  }
}

resource privateDnsZonePrivatelinkNameFhir_workspaceName_workspace 'Microsoft.Network/privateDnsZones/A@2020-06-01' = {
  parent: privateDnsZonePrivatelinkNameFhir
  name: '${workspaceName}-workspace'
  properties: {
    metadata: {
      creator: 'created by private endpoint'
    }
    ttl: 10
    aRecords: [
      {
        ipv4Address: '10.0.0.4'
      }
    ]
  }
}

resource Microsoft_Network_privateDnsZones_SOA_privateDnsZonePrivatelinkNameFhir 'Microsoft.Network/privateDnsZones/SOA@2020-06-01' = {
  parent: privateDnsZonePrivatelinkNameFhir
  name: '@'
  properties: {
    ttl: 3600
    soaRecord: {
      email: 'azureprivatedns-host.microsoft.com'
      expireTime: 2419200
      host: 'azureprivatedns.net'
      minimumTtl: 10
      refreshTime: 3600
      retryTime: 300
      serialNumber: 1
    }
  }
}

resource Microsoft_Network_privateDnsZones_SOA_privateDnsZonePrivatelinkNameDicom 'Microsoft.Network/privateDnsZones/SOA@2020-06-01' = {
  parent: privateDnsZonePrivatelinkNameDicom
  name: '@'
  properties: {
    ttl: 3600
    soaRecord: {
      email: 'azureprivatedns-host.microsoft.com'
      expireTime: 2419200
      host: 'azureprivatedns.net'
      minimumTtl: 10
      refreshTime: 3600
      retryTime: 300
      serialNumber: 1
    }
  }
}
