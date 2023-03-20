@description('Admin password for SQL.')
@minLength(12)
@secure()
param adminPassword string

@description('Log in name to use.')
param adminLogin string = 'student'

@description('Location for all resources.')
param location string = resourceGroup().location

@description('Vnet Name')
param vnetName string

@description('Container group name')
param containerGroupName string = 'contoso-qvera-containergroup'

@description('Container name')
param containerName string = 'qie-container'

@description('Container image to deploy. Should be of the form accountName/imagename:tag for images stored in Docker Hub or a fully qualified URI for a private registry like the Azure Container Registry.')
param image string = 'qvera/qie:5.0.50'

@description('Port to open on the container.')
param port int = 80

@description('The number of CPU cores to allocate to the container. Must be an integer.')
param cpuCores int = 4

@description('The amount of memory to allocate to the container in gigabytes.')
param memoryInGb int = 16

@description('Subnet Prefix')
param subnetPrefix string = '10.0.1.0/24'

@description('Subnet Name')
param subnetName string = 'qveraSubnet'



var networkProfileName = '${subnetName}-networkProfile' //'aci-networkProfile'
var interfaceConfigName = '${subnetName}-eth0'
var interfaceIpConfig = '${subnetName}-ipconfigprofile1'

// This defines a resource for an EXISTING vnet! (must already exist)
resource vnet 'Microsoft.Network/virtualNetworks@2021-03-01' existing = {
  name: vnetName
}

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

// Deploy SQL (Please note that it's NOT secure as we're using a password to connect)
module sql './create-sql-db-for-qie.bicep' = {
  name: 'sqldb'
  params: {
    location: location
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    sqlDBName: 'qie'
  }
}

resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2019-12-01' = {
  name: containerGroupName
  location: location
  dependsOn: [
    vnet
    subnet
    sql
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
              name: 'JAVA_OPTIONS'
              value: '-Xmx4096m'
            }
            {
              name: 'connection_driver'
              value: 'com.microsoft.sqlserver.jdbc.SQLServerDriver'
            }
            {
              name: 'connection_url'
              value: 'jdbc:sqlserver://${sql.outputs.sqlServerName}.database.windows.net:1433;database=qie;integratedSecurity=false'
            }
            {
              name: 'connection_username'
              value: 'student@${sql.outputs.sqlServerName}'
            }
            {
              name: 'connection_password'
              value: adminPassword
            }
            {
              name: 'hibernate_dialect'
              value: 'com.qvera.qie.persistence.SQLServer2019UnicodeDialect'
            }
          ]
          volumeMounts: [
            {
              name: 'copygitrepo'
              mountPath: '/tmp/database/'
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
    volumes: [
      {
        name: 'copygitrepo'
        gitRepo: {
          repository: 'https://github.com/StevenBorg/ahds_demo_config'
          directory: '.'
        } 
      }
    ]
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


//output vm string = jumpbox_deployment.outputs.hostname
//output vnet string = jumpbox_deployment.outputs.vnetName
//output qveraSubnetName string = add_subnet.outputs.subnetName
//output qveraSubnetId string = add_subnet.outputs.subnetId

output subnetName string = subnet.name
output subnetId string = subnet.id
output networkProfileName string = networkProfile.name
output networkProfileId string = networkProfile.id
