
// This script deploys the on-prem solution 
@description('Administrator Password for Orthanc.')
@minLength(12)
@secure()
param adminPassword string

@description('Location for all resources.')
param location string = resourceGroup().location

@description('Subnet Name')
param vnetName string = 'ContosoVnet'

@description('Login name for Jump VM and SQL.')
param adminLogin string = 'student'

@description('Qvera QIE ontainer image to deploy.')
param image string = 'qvera/qie:latest'

@description('Port to open on the container.')
param port int = 80

@description('The number of CPU cores to allocate to the container. Must be an integer.')
param qveraCpuCores int = 4

@description('The amount of memory to allocate to the container in gigabytes.')
param qveraMemoryInGb int = 16

@description('Size of the virtual machine.')
@allowed([
  'Standard_DS1_v2'
  'Standard_D2s_v5'
])
param vmSize string = 'Standard_DS1_v2' //'Standard_D2s_v5'


// Deploy the jumpbox and vnet
module jumpbox_deployment './deploy-vnet-with-jump-vm.bicep' = {
  name: 'jumpbox_deployment'
  params: {
    location: location
    adminPassword: adminPassword
    vnetName: vnetName
    adminUsername: adminLogin
    vmSize: vmSize
  }
}

// This defines a resource for the vnet created above
resource vnet 'Microsoft.Network/virtualNetworks@2021-03-01' existing = {
  name: jumpbox_deployment.outputs.vnetName    
}

// Deploy qvera into a new subnet
module qvera_subnet './deploy-qvera-subnet.bicep' = {
  name: 'qvera_subnet'
  dependsOn: [
    vnet
  ]
  params: {
    location: location
    vnetName: jumpbox_deployment.outputs.vnetName
    adminPassword: adminPassword
    adminLogin: adminLogin
    containerName: 'qie-container'
    cpuCores: qveraCpuCores
    memoryInGb: qveraMemoryInGb
    subnetName: 'qveraSubnet'
    subnetPrefix: '10.0.1.0/24'
    image: image
    port: port
  }
}

// Deploy orthanc into a new subnet
module orthanc_subnet './deploy-orthanc-subnet.bicep' = {
  name: 'orthanc_subnet'
  dependsOn: [
    vnet
    qvera_subnet
  ]
  params: {
    location: location
    vnetName: jumpbox_deployment.outputs.vnetName
    containerName: 'orthanc-container'
    cpuCores: 2
    memoryInGb: 4
    subnetName: 'orthancSubnet'
    subnetPrefix: '10.0.2.0/24'
  }
}
