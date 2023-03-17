// Bicep file to deploy OHIF and connect to an existing DICOM service
//    Used by other deployments, such as deploying OHIF with a DICOM service

@description('Specifies the location for resources.')
param location string = resourceGroup().location

@description('Your existing DICOM service URL (format: https://yourworkspacename-yourdicomservicename.dicom.azurehealthcareapis.com)')
param dicomServiceUrl string //= 'https://sjbrsnadicomws-rsnadicom.dicom.azurehealthcareapis.com'

@description('Your existing Azure AD tenant ID (format: 72xxxxxf-xxxx-xxxx-xxxx-xxxxxxxxxxx)')
//@secure()
param aadTenantId string //= '72f988bf-86f1-41af-91ab-2d7cd011db47'

@description('Your existing Application (client) ID (format: 1f8xxxxx-dxxx-xxxx-xxxx-9exxxxxxxxxx)')
//@secure()
param applicationClientId string //= 'fd1caac5-b104-4709-8bbf-747e3f39ce9a'

@description('Your existing Application (client) secret')
@secure()
param applicationClientSecret string 

@description('Container image to deploy. Should be of the form accountName/imagename:tag for images stored in Docker Hub or a fully qualified URI for a private registry like the Azure Container Registry.')
param image string = 'stevenborg/meddream:latest'

@description('Port to open on the container.')
param port int = 8080

@description('The number of CPU cores to allocate to the container. Must be an integer.')
param cpuCores int = 4

@description('The amount of memory to allocate to the container in gigabytes.')
param memoryInGb int = 16

resource meddreamContainerGroup 'Microsoft.ContainerInstance/containerGroups@2019-12-01' = {
  name: 'meddreamContainerGroup'
  location: location
  properties: {
    containers: [
      {
        name: 'meddream'
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
              name: 'integration'
              value: 'study'
            }
            {
              name: 'azuredicomurl'
              value: dicomServiceUrl
            }
            {
              name: 'azuretenantid'
              secureValue: aadTenantId
            }
            {
              name: 'azureappid'
              secureValue: applicationClientId
            }
            {
              name: 'azureappsecret'
              secureValue: applicationClientSecret
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
    ipAddress: {
      type: 'Public'
      ports: [
        {
          port: port
          protocol: 'TCP'
        }
      ]
    }
    osType: 'Linux'

    restartPolicy: 'Always'
  }
}

output meddreamIp string = meddreamContainerGroup.properties.ipAddress.ip
//output meddreamFqdn string = meddreamContainerGroup.properties.ipAddress.fqdn
output meddreamPort array = meddreamContainerGroup.properties.ipAddress.ports
