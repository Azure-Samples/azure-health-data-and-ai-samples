param storageAccountName string
param location string
param appTags object = {}

param hl7containername string
var validatedblobcontainer = 'hl7-validation-succeeded'
var hl7validationfailblobcontainer = 'hl7-validation-failed'
var hl7skippedcontainer = 'hl7-skipped'
var hl7resynchronizationcontainer = 'hl7-sequence-resync'
var convertedcontainer = 'hl7-converter-succeeded'
var conversionfailcontainer = 'hl7-converter-failed'
var hl7converterjsoncontainer = 'hl7-converter-json'
var hl7postprocesscontainer = 'hl7-postprocess-json'
var processedblobcontainer = 'hl7-fhirupload-succeeded'
var hl7failedblob = 'failedhl7'
var failedblobcontainer = 'hl7-fhirupload-failed'
var fhirfailedblob = 'hl7-fhirupload-failed'
var skippedblobcontainer = 'hl7-skipped'
var fhirjsoncontainer = 'hl7-converter-json'
var validatedcontainer = 'hl7-validation-succeeded'
var hl7fhirpostporcessjson = 'hl7-postprocess-json'



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
    name: '${storageAccountName}/default/${hl7containername}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        metadata: {}
        publicAccess: 'None'
    }
}

@description('Name of the container where hl7 validated files be will store')
resource validatedblobcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${validatedblobcontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        metadata: {}
        publicAccess: 'None'
    }
}

@description('Name of the container where hl7 validation failed files will store')
resource hl7validationfailblobcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${hl7validationfailblobcontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        publicAccess: 'None'
        metadata: {}
    }
}

@description('Name of the container where hl7 skipped files will store')
resource hl7skippedcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${hl7skippedcontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        publicAccess: 'None'
        metadata: {}
    }
}

@description('Name of the container where hl7 resynchronized hl7 files will store')
resource hl7resyncontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${hl7resynchronizationcontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        publicAccess: 'None'
        metadata: {}
    }
}

@description('Name of the container where converted files will store')
resource convertedcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${convertedcontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        publicAccess: 'None'
        metadata: {}
    }
}

@description('Name of the container where hl7 conversion failed files will store')
resource conversionfailcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${conversionfailcontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        publicAccess: 'None'
        metadata: {}
    }
}


@description('Name of the container where hl7 file converted into FHIR json will store')
resource hl7converterjsoncontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${hl7converterjsoncontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        publicAccess: 'None'
        metadata: {}
    }
}


@description('Name of the container where Fhir json after post process will be stored')
resource hl7postprocesscontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${hl7postprocesscontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        publicAccess: 'None'
        metadata: {}
    }
}

@description('Name of the container where processed files will store')
resource processedblobcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${processedblobcontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        publicAccess: 'None'
        metadata: {}
    }
}


@description('Name of the container where fhir upload failed files will store')
resource hl7failedblobs 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${hl7failedblob}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        metadata: {}
        publicAccess: 'None'
    }
}

@description('Name of the container where failed files will store')
resource failedblobcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${failedblobcontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        metadata: {}
        publicAccess: 'None'
    }
}

@description('Name of the container where fhir upload failed files will store')
resource fhirfailedblobs 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${fhirfailedblob}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        metadata: {}
        publicAccess: 'None'
    }
}


@description('Name of the container where hl7 skipped files will store')
resource skippedblobcontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${skippedblobcontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        metadata: {}
        publicAccess: 'None'
    }
}


@description('Name of the container where hl7 file converted into FHIR json will store')
resource fhirjsoncontainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${fhirjsoncontainer}'
    dependsOn: [
        funcStorageAccount
    ]
    properties: {
        metadata: {}
        publicAccess: 'None'
    }
}


@description('Name of the container where Fhir json after post process will be stored')
resource hl7fhirpostporcessjsons 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
    name: '${storageAccountName}/default/${hl7fhirpostporcessjson}'
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

output accountkey string = funcStorageAccount.listKeys().keys[0].value
output hl7containername string = hl7containername
output validatedblobcontainer string = validatedblobcontainer
output hl7validationfailblobcontainer string = hl7validationfailblobcontainer
output hl7skippedcontainer string = hl7skippedcontainer
output hl7resynchronizationcontainer string = hl7resynchronizationcontainer
output convertedcontainer string = convertedcontainer
output conversionfailcontainer string = conversionfailcontainer
output hl7converterjsoncontainer string = hl7converterjsoncontainer
output hl7postprocesscontainer string = hl7postprocesscontainer
output processedblobcontainer string = processedblobcontainer
output hl7failedblob string = hl7failedblob
output failedblobcontainer string = failedblobcontainer
output fhirfailedblob string = fhirfailedblob
output skippedblobcontainer string = skippedblobcontainer
output fhirjsoncontainer string = fhirjsoncontainer
output hl7fhirpostporcessjson string = hl7fhirpostporcessjson
output validatedcontainer string = validatedcontainer