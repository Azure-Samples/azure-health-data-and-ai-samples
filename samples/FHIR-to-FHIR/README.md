# FHIR-to-FHIR Data Movement: Moving data from one Azure API for FHIR server to another.

This sample will focus on how to move FHIR data from one Azure API for FHIR server to another Azure API for FHIR server. This sample app utilizes [$export](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data) (which allows you to filter and export certain data according to your [query](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data#query-parameters)) for export, and [FHIR Loader](https://github.com/microsoft/fhir-loader) for import. 
Note: This tutorial does not apply for the new Azure Health Data Services FHIR service. For more information on the different FHIR capabilities from Microsoft, see [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/overview#fhir-platforms-from-microsoft).

## Prerequisites needed
1.	Microsoft work or school account
2.	2 Azure API for FHIR instances.
	-	**Source**: One instance from where the data will be exported from.
	-	**Destination**: Second instance where the data will be imported to.


# Steps
1. Deploy the FHIR Bulk Loader. Go to [FHIR Bulk Loader](https://github.com/microsoft/fhir-loader) and follow the [deployment steps](https://github.com/microsoft/fhir-loader#deployment). It will create the following resources:
	- Resource Group
	- Storage Account
	- App Service Plan
	- Function App
	- Event Grid
	- Key Vault
	- Application Insight

	The FHIR Bulk Loader will be linked to the **destination** Azure API for FHIR server on which the data will be imported.
2. Export data from the source Azure API for FHIR server.
	- Follow steps [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) to configure export on Azure API for FHIR server.
	- Once the export configuration is setup, run the export command on Azure API for FHIR server.
	Follow the [steps](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data) to run the export command.\
	Example:
	``` PowerShell
	GET https://<<Source FHIR Server URL>>/$export?_container=<<CONTAINER NAME>>
	```
	- The instructions also list [query parameters](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data#query-parameters) that can be used to filter what data gets exported
	- The exported data will be in the format of NDJSON files that are stored in a new container that was created during the export configuration process.

3. Run the provided sample PowerShell script

	Running the PowerShell script will do the following process:

	1. Batch processed files specific folder in the Azure Storage Account.
	2. Copy the NDJSON files from the source storage account to the destination storage account which is linked to FHIR Bulk Loader.

	To run the PowerShell Script, perform the following steps:

	1. Clone this [FHIR-to-FHIR]() repo to your local machine.
	2. Open the PowerShell console, ensure that you have the latest version of the **PowerShell 7** or **Powershell 5.1**.
	3. If you don't already have Powershell Az and Azure CLI, install Powershell [Az](https://docs.microsoft.com/en-us/powershell/azure/install-az-ps?view=azps-7.1.0) and [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli).
	4. Sign in to your Azure account to the subscription where synapse is located.
		``` PowerShell
		Connect-AzAccount -SubscriptionId 'yyyy-yyyy-yyyy-yyyy'
		```
	5. Browse to the scripts folder under this path (..\FHIR-to-FHIR\scripts).
	6. Run the following PowerShell script. The parameters are detailed in the table below.
		```Powershell
		./BatchProcess.ps1 -srcResourceGroup '<Source Resource Group Name>'-srcStorageAccount '<Source Storage Account Name>' -destResourceGroup '<Destination Resource Group Name>' -destStorageAccount '<Destination Storage Account Name>' -sourceContainer '<Source Container Name>' -FileCount '<# of File to process in a batch>' -BundleCount '<# of Bundle files in bundle container>'
		```
		|Parameter   | Description   |
        |---|---|
        | srcResourceGroup | Name of the source resource group where the data from Azure API for FHIR is exported. |
        | srcStorageAccount | Name of the storage account where the data from Azure API for FHIR is exported. |
        | sourceContainer | Name of the container where the data from Azure API for FHIR is exported. |
        | destResourceGroup | Name of the resource group where FHIR Bulk Loader is deployed. |
        | destStorageAccount | Name of the storage account where all the containers under FHIR Bulk Loader are created. |
        | FileCount | Number of the file count that will be processed in a single batch. |
        | BundleCount | Number of the bundle files in destination storage container 'bundles'. Next Batch will trigger when the number of bundle file is below this number. 
