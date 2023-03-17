@description('Specifies the location for resources.')
param location string = resourceGroup().location

@description('Name of the AHDS workspace. This will appear in the API URL.')
param workspace_name string = 'workspace${uniqueString(resourceGroup().id)}'

@description('Name of the AHDS DICOM service. This will appear in the API URL.')
param dicom_service_name string = 'dicom${uniqueString(resourceGroup().id)}'

@description('Name of the AHDS FHIR service. This will appear in the API URL.')
param fhir_service_name string = 'fhir${uniqueString(resourceGroup().id)}'

@description('Should deploy FHIR')
param should_deploy_fhir bool = false

@description('Should deploy DICOM')
param should_deploy_dicom bool = true

// @description('Your existing DICOM service URL (format: https://yourworkspacename-yourdicomservicename.dicom.azurehealthcareapis.com)')
// param dicomServiceUrl string = 'https://sjbrsnadicomws-rsnadicom.dicom.azurehealthcareapis.com'

@description('Your existing Azure AD tenant ID (format: 72xxxxxf-xxxx-xxxx-xxxx-xxxxxxxxxxx)')
@secure()
param aadTenantId string 

@description('Your existing Application (client) ID (format: 1f8xxxxx-dxxx-xxxx-xxxx-9exxxxxxxxxx)')
@secure()
param applicationClientId string 

@description('Your existing Application (client) secret')
@secure()
param applicationClientSecret string 

// This is VERY confusing! And you can't get the Principal Object ID from the Portal UI. 
//    See https://github.com/Azure/terraform-azurerm-appgw-ingress-k8s-cluster/issues/1 ') for how to get it

// When adding role assignments, and get an error: Principals of type Application cannot validly be used in role as...
// 	that means you're using the Application's Object ID. You need to find the Application's associated Principal Object ID...  
// see: https://github.com/Azure/terraform-azurerm-appgw-ingress-k8s-cluster/issues/1
// and: https://github.com/Azure/azure-cli/issues/5340
// Basically run: az ad sp list --filter "displayName eq 'rsna-demo-viewer-app1'" and get the id provided there - of course with your Application ID name
@description('Object ID of the Principal of the Application - confusing - see https://github.com/Azure/terraform-azurerm-appgw-ingress-k8s-cluster/issues/1 ')
@secure()
param dicom_principalId string //= '7e086154-4646-4550-b350-f94fadc6720b' // Object ID of rsna app reg

@description('Container image to deploy. Should be of the form accountName/imagename:tag for images stored in Docker Hub or a fully qualified URI for a private registry like the Azure Container Registry.')
param image string = 'stevenborg/meddream:latest'

@description('Port to open on the container.')
param port int = 8080

@description('The number of CPU cores to allocate to the container. Must be an integer.')
param cpuCores int = 4

@description('The amount of memory to allocate to the container in gigabytes.')
param memoryInGb int = 16

// @description('Storage blob name')
// param storage_blob_name string = 'storage${uniqueString(resourceGroup().id)}'

// @description('Desired name of the storage account instance')
// param storageAccountName string

// @description('Your existing DICOM service URL (format: https://yourworkspacename-yourdicomservicename.dicom.azurehealthcareapis.com)')
// param dicomServiceUrl string = '<The URL to your DICOM service>'

@description('Should add DICOM App Registration')
param should_add_dicom_app_registration bool = true

    

// var tenantId = tenant().tenantId
// var loginURL = environment().authentication.loginEndpoint
// var authority = '${loginURL}${tenantId}'
// var audience = 'https://${workspace_name}-${fhir_service_name}.fhir.azurehealthcareapis.com'
// var dicom_roleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions','58a3b984-7adf-4c20-983a-32417c86fbc8')

//var full_storage_name = '${storage_account_name}/${storage_blob_name}'

module ahds_deployment './deploy-dicom-and-fhir-services.bicep' = {
  name: 'ahds_deployment'
  params: {
    location: location
    workspace_name: workspace_name
    dicom_service_name: dicom_service_name
    fhir_service_name: fhir_service_name
    should_deploy_dicom: should_deploy_dicom
    should_deploy_fhir: should_deploy_fhir
    dicom_principalId: dicom_principalId
    should_add_dicom_app_registration: should_add_dicom_app_registration
  }
}

module meddream_deployment './deploy-meddream.bicep' = {
  name: 'meddream_deployment'
  params: {
    location: location
    applicationClientSecret: applicationClientSecret
    aadTenantId: aadTenantId
    applicationClientId: applicationClientId
    dicomServiceUrl: ahds_deployment.outputs.dicom_uri
    port: port
    cpuCores: cpuCores
    memoryInGb: memoryInGb
    image: image

  }
  dependsOn: [
    ahds_deployment
  ]
}

output workspace_name string = workspace_name
output dicom_service_name string = dicom_service_name
output fhir_service_name string = fhir_service_name
output dicom_uri string = ahds_deployment.outputs.dicom_uri
output fhir_uri string = ahds_deployment.outputs.fhir_uri
output fhir_deployed bool = should_deploy_fhir
output dicom_deployed bool = should_deploy_dicom

output meddreamIp string = meddream_deployment.outputs.meddreamIp
//output meddreamFqdn string =meddream_deployment.outputs.meddreamFqdn
//output meddreamPort object = meddream_deployment.outputs.meddreamPort

