@description('Location for all resources.')
param location string = resourceGroup().location

@description('Vnet Name')
param vnetName string

@description('Container group name')
param containerGroupName string = 'contoso-orthanc-containergroup'

@description('Container name')
param containerName string = 'orthanc-container'

@description('Container image to deploy. Should be of the form accountName/imagename:tag for images stored in Docker Hub or a fully qualified URI for a private registry like the Azure Container Registry.')
param image string = 'osimis/orthanc:22.6.1-full'

@description('Port to open on the container.')
param port int = 80

@description('The number of CPU cores to allocate to the container. Must be an integer.')
param cpuCores int = 2

@description('The amount of memory to allocate to the container in gigabytes.')
param memoryInGb int = 8

@description('Subnet Prefix')
param subnetPrefix string = '10.0.2.0/24'

@description('Subnet Name')
param subnetName string = 'orthancSubnet'

// This defines a resource for an EXISTING vnet! (must already exist)
resource vnet 'Microsoft.Network/virtualNetworks@2021-03-01' existing = {
  name: vnetName
}

var networkProfileName = '${subnetName}-networkProfile' 
var interfaceConfigName = '${subnetName}-eth0'
var interfaceIpConfig = '${subnetName}-ipconfigprofile1'


resource subnet 'Microsoft.Network/virtualNetworks/subnets@2020-11-01' = {
  name: subnetName
  parent: vnet
  properties: {
    addressPrefix: subnetPrefix
    delegations: [
      {
        name: 'DelegationService'
        properties: {
          serviceName: 'Microsoft.ContainerInstance/containerGroups'
        }
      }
    ]
  }
}

// Network profiles are automatically created, but it was in the demo, so I copied it over...
resource networkProfile 'Microsoft.Network/networkProfiles@2020-11-01' = {
  name: networkProfileName
  location: location
  properties: {
    containerNetworkInterfaceConfigurations: [
      {
        name: interfaceConfigName
        properties: {
          ipConfigurations: [
            {
              name: interfaceIpConfig
              properties: {
                subnet: {
                  id: subnet.id
                }
              }
            }
          ]
        }
      }
    ]
  }
}

resource orthancContainerGroup 'Microsoft.ContainerInstance/containerGroups@2019-12-01' = {
  name: containerGroupName
  location: location
  dependsOn: [
    subnet
  ]
  properties: {
    containers: [
      {
        name: containerName
        properties: {
          image: image
          ports: [
            {
              port: port
              protocol: 'TCP'
            }
          ]
          environmentVariables: [
            {
              name: 'ORTHANC__NAME'
              value: 'orthanc'
            }
            {
              name: 'ORTHANC__REGISTERED_USERS'
              value: '{"student":"student"}'
            }
            {
              name: 'WVB_ENABLED'
              value: 'true'
            }
            {
              name: 'ORTHANC__DICOM_AET'
              value: 'ORTHANC'
            }
            {
              name: 'ORTHANC__DICOM_CHECK_CALLED_AET'
              value: 'false'
            }
            {
              name: 'ORTHANC__DICOM_PORT'
              value: '4242'
            }
            {
              name: 'ORTHANC__DEFAULT_ENCODING'
              value: 'Latin1'
            }
            {
              name: 'ORTHANC__DICOM_MODALITIES'
              value: '{ "QIETOAZURE" : [ "QIETOAZURE", "10.0.1.4", 4006 ] }'
            }
            {
              name: 'ORTHANC__DICOM_THREADS_COUNT'
              value: '40'
            }
            {
              name: 'ORTHANC__CONCURRENT_JOBS'
              value: '0'
            }
          ]
          resources: {
            requests: {
              cpu: cpuCores
              memoryInGB: memoryInGb
            }
          }
        }
      } 
    ]
    // volumes: [
    //   {
    //     name: 'myvolume'
    //     gitRepo: {
    //       repository: 'https://github.com/StevenBorg/ahds_demo_config'
    //       directory: '.'
    //     } 
    //   }
    // ]
    osType: 'Linux'
    // With this commented out, we can't get the containerGroup.properties.ipAddress.ip value
    //  But this causes an error with the jump vm script
    //  WHOOAA!!!  Without this the container-group doesn't GET an ip address!
    networkProfile: {
      id: networkProfile.id   
    }
    restartPolicy: 'Always'
  }
}

output subnetName string = subnet.name
output subnetId string = subnet.id
output networkProfileName string = networkProfile.name
output networkProfileId string = networkProfile.id

