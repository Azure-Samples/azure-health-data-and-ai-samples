param storageAccountName string
param location string
param appTags object = {}
var processedfilescontainer ='processed'
var convertedfilescontainer ='converted'
var failedvalidationfilescontainer ='hl7validationfailed'
var failedconversioncontainer ='conversionfail'
var faileduploadcontainer ='fhiruploadfail'
var skippedfilecontainer ='skippedforerror'
var validatedcontainer = 'validated'
var hl7resyncontainer = 'hl7resynchronization'
param hl7continername string


@description('Azure Function required linked storage account')
resource funcStorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  tags: appTags
}

@description('Name of the container where hl7 files will be uploaded ')
resource hl7filescontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccountName}/default/${hl7continername}'
  dependsOn: [
    funcStorageAccount
  ]
  properties: {    
    metadata: {}
    publicAccess: 'None'
  }
}

@description('Name of the container where hl7 validated files be will store')
resource validatedcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccountName}/default/${validatedcontainer}'
  dependsOn: [
    funcStorageAccount
  ]
  properties: {    
    metadata: {}
    publicAccess: 'None'
  }
}

@description('Name of the container where hl7 validation failed files will store')
resource failedvalidationcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccountName}/default/${failedvalidationfilescontainer}'
  dependsOn: [
    funcStorageAccount
  ]
  properties: {
    publicAccess: 'None'
    metadata: {}
  }
}

@description('Name of the container where hl7 resynchronized files will store')
resource hl7resyncontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccountName}/default/${hl7resyncontainer}'
  dependsOn: [
    funcStorageAccount
  ]
  properties: {
    publicAccess: 'None'
    metadata: {}
  }
}

@description('Name of the container where converted files will store')
resource  convertedcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccountName}/default/${convertedfilescontainer}'
  dependsOn: [
    funcStorageAccount
  ]
  properties: {
    publicAccess: 'None'
    metadata: {}
  }
}

@description('Name of the container where hl7 conversion failed files will store')
resource failedconversationcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccountName}/default/${failedconversioncontainer}'
  dependsOn: [
    funcStorageAccount
  ]
  properties: {
    publicAccess: 'None'
    metadata: {}
  }
}

@description('Name of the container where skipped files will store')
resource skippedconversationcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccountName}/default/${skippedfilecontainer}'
  dependsOn: [
    funcStorageAccount
  ]
  properties: {
    publicAccess: 'None'
    metadata: {}
  }
}

@description('Name of the container where hl7 upload failed files will store')
resource faileduploadcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccountName}/default/${faileduploadcontainer}'
  dependsOn: [
    funcStorageAccount
  ]
  properties: {
    publicAccess: 'None'
    metadata: {}
  }
}

@description('Name of the container where processed will store')
resource processedfilescontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccountName}/default/${processedfilescontainer}'
  dependsOn: [
    funcStorageAccount
  ]
  properties: {    
    metadata: {}
    publicAccess: 'None'
  }
}




output accountkey string = funcStorageAccount.listKeys().keys[0].value
output hl7filescontainer string = hl7continername
output validatedcontainer string = validatedcontainer
output hl7failedvalidationfilescontainer string = failedvalidationfilescontainer
output hl7resyncontainers string = hl7resyncontainer
output hl7convertedfilescontainer string = convertedfilescontainer
output hl7Failedcontainer string = failedconversioncontainer
output hl7skippedfilecontainer string = skippedfilecontainer
output hl7failedfilescontainer string = faileduploadcontainer
output hl7processedfilescontainer string = processedfilescontainer


