# FHIR-to-FHIR Data Movement: Moving data from one Azure API for FHIR server to another.

This sample will focus on how to move the data from Azure API for FHIR (Generation-1) server to another Azure API for FHIR (Generation-1)server.

## Prerequisites needed
1.	Microsoft work or school account
2.	Azure API for FHIR instances.
	-	We will require 2 FHIR instance. 
	-	One from where the data will be exported.
	-	Second instance where the data will be imported.


# Steps for the data movement between servers

1. Go to [FHIR Bulk Loader](https://github.com/microsoft/fhir-loader) and follow the deployment steps.
2. Follow the [steps](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) for exporting the data from Azure API for FHIR server.
3. Go to the script folder.
4. Run the below command to copy the exported data to FHIR bulk loader.

```
.\BatchProcess.ps1 ` 
-srcResourceGroup '<Source Resource Group Name>' `
-srcStorageAccount '<Source Storage Account Name>' `
-destResourceGroup '<Destination Resource Group Name>' `
-destStorageAccount '<Destination Storage Account Name>' `
-sourceContainer '<Source Container Name>'
```    

