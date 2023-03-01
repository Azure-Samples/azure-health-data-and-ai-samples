# FHIR-to-FHIR Data Movement: Moving data from one Azure API for FHIR server to another.

This sample will focus on how to move FHIR data from one Azure API for FHIR server to another Azure API for FHIR server. This sample app utilizes [$export](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data) (which allows you to filter and export certain data according to your [query](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data#query-parameters)) to export data from a source Azure API for FHIR server, and [FHIR Loader](https://github.com/microsoft/fhir-loader) to import to the destination Azure API for FHIR server. 
Note: This tutorial is only for Azure API for FHIR and does not apply for the new Azure Health Data Services FHIR service. For more information on the different FHIR capabilities from Microsoft, see [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/overview#fhir-platforms-from-microsoft).

# Architecture Overview

![Architecture](docs/images/Architecture.png)

# Prerequisites needed
1.	Microsoft work or school account
2.	Two Azure API for FHIR instances.
	-	**Source**: One instance from where the data will be exported from.
	-	**Destination**: Second instance where the data will be imported to.

# Deployment
1. Deploy the FHIR Bulk Loader. Go to [FHIR Bulk Loader](https://github.com/microsoft/fhir-loader) and follow the [deployment steps](https://github.com/microsoft/fhir-loader#deployment).

	**NOTE**: Pass the **destination** Azure API for FHIR server details while deploying the FHIR Bulk Loader
	
	Once FHIR loader is deployed, it will create the following resources:
	- Resource Group
	- Storage Account (container/queues)
	- App Service Plan
	- Function App
	- Event Grid
	- Key Vault
	- Application Insight
	
	The FHIR Bulk Loader will be linked to the **destination** Azure API for FHIR server on which the data will be imported.

2. Export data from the **source** Azure API for FHIR server.
	- Follow steps [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) to configure export on Azure API for FHIR server.
	- Once the export configuration is setup, run the export command on Azure API for FHIR server.
	Follow the [steps](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data) to run the export command.\
	**Examples**:  
		If you'd like to run a system export:
		``` PowerShell
		GET https://<<Source FHIR Server URL>>/$export?_container=<<CONTAINER NAME>>
		```
		If you'd like to export per resource:
		``` PowerShell
		GET https://<<Source FHIR Server URL>>/$export?_container=<<CONTAINER NAME>>&_type=<<RESOURCE TYPE>>
		```
		**NOTE** : If you export per resource, you will need to manually run the above command once per resource type. Execute the export jobs in parallel to minimize wait times, and note down the job IDs to check the execution status for each export job.

	- The instructions also list [query parameters](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data#query-parameters) that can be used to filter what data gets exported.
	- The exported data will be in the format of NDJSON files that are stored in a new container which was created during the export configuration process.

3. Provide privilege to your account.
	You must provide the following roles to your account to run the PowerShell script in the next step.
	- In the Storage Accounts (Source/Destination) created during the above steps, select the Access Control (IAM) and assign the **Storage Blob Data Contributor** role to your account.

4. Using FHIR Loader to import data

	 - Running the PowerShell script will do the following process:

		1. Copy the NDJSON files from the source storage account container to the destination storage account container ('ndjson' container) which is linked to FHIR Bulk Loader.
		2. Once the data is copied to the destination storage container the FHIR Bulk Loader will start importing the data to linked Azure API for FHIR server.

	- To run the PowerShell Script, perform the following steps:

		1. Clone this [FHIR-to-FHIR]() repo to your [Open Azure Cloud Shell](https://shell.azure.com) - you can also access this from [Azure Portal](https://portal.azure.com).
		2. Sign into your Azure account
			``` PowerShell
			az account set -s 'xxxx-xxxx-xxxx-xxxx-xxxxxx'
			```
			where 'xxxx-xxxx-xxxx-xxxx-xxxxxx' is your subscription ID.

		3. Login to Azcopy with tenant id using below command
			```Powershell
			azcopy login --tenant-id 'xxxx-xxxx-xxxx-xxxx'  
			```
		4. Browse to the scripts folder under this path (..\fhir-to-fhir\api-for-fhir-to-api-for-fhir\scripts).

		5. Run the following PowerShell script. 
			```Powershell
			./CopyFiles.ps1 -src '<Source NDJSON files container link>'-dest '<Destination NDJson files container link>' 
			```
			|Parameter   | Description   |
			|---|---|
			| src | HTTP link of the source container where the data from source Azure API for FHIR is exported. |
			| dest | HTTP link of the destination container which is connected to FHIR Bulk Loader. 

			Example:
			``` PowerShell
			./CopyFiles.ps1 -src "https://<Source_Storage_Name>.blob.core.windows.net/<Container_Name>/<Sub_Directory>/*" -dest "https://<Destination_Storage_Name>.blob.core.windows.net/ndjson/"
			```
	- Logging
		- During the PowerShell script execution, log files will be created under the location : 
			- ..\fhir-to-fhir\api-for-fhir-to-api-for-fhir\scripts\logfiles.
		- It logs the output of copy operation performed during script execution.
		- For every execution of PowerShell script new logs will be created under the folder "api-for-fhir-to-api-for-fhir\scripts\logfiles".

# Destination FHIR server and FHIR Bulk Loader App configuration.

To move data from one FHIR server to another, we can configure settings for destination FHIR server as per the data we need to move, for example the number of RUs and nodes needed for the FHIR server.

We can configure settings for the FHIR Loader application according to the data it needs to process, such as number of resources per bundle files or App service plan and instance nodes required during process.

To make FHIR server and FHIR Bulk Loader App setting configurations, please follow these [steps](https://github.com/Azure-Samples/azure-health-data-services-samples/blob/snarang/fhir2fhir/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir/docs/Server_%26_App_Config.md).

# Error handling

During the data movement, errors might occur. It can be during exporting data or copying the data to destination account, or errors while importing data in FHIR Bulk Loader.

To handle errors that occurr during the process, please follow these [steps](https://github.com/Azure-Samples/azure-health-data-services-samples/blob/snarang/fhir2fhir/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir/docs/Error_Handling.md).

