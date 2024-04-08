# Migrate your Custom Search Parameter from Azure API for FHIR server to Azure Health Data Services FHIR service using the Lift and Shift migration pattern

This sample will focus on how to migrate the FHIR custom search parameter from Azure API for FHIR server to Azure Health Data Services FHIR service using the Lift and Shift migration pattern.

# Prerequisites needed
1.  Microsoft work or school account
2.  PowerShell Verion 7 and above
3.	FHIR instances.
	-	**Source**: Azure API for FHIR server instance from where the data will be exported from.
		- Have the Azure API for FHIR server URL handy:
			```PowerShell
			https://<<SOURCE_ACCOUNT_NAME>>.azurehealthcareapis.com/
			```
	-	**Destination**: Azure Health Data Services FHIR service instance where the data will be imported to. See [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/get-started-with-fhir) for instructions on creating a new Azure Health Data Services FHIR service (and associated Azure Health Data Services [workspace](https://docs.microsoft.com/azure/healthcare-apis/healthcare-apis-quickstart)) if you don't already have one. 
		- Have the Azure Health Data Service FHIR service URL handy:
			```PowerShell
			https://<<WORKSPACE_NAME>>-<<FHIR_SERVICE_NAME>>.fhir.azurehealthcareapis.com/
			```
## Steps

The PowerShell script will take the Azure API for FHIR server custom search parameter resources and will post it on Azure Health Data Services FHIR service.

- To run the PowerShell Script, you need to have the "FHIR Data Contributor" role for the Azure API for FHIR and Azure Health Data Services FHIR Service, as the script require access to both source and destination server. Follow [steps](https://learn.microsoft.com/azure/healthcare-apis/configure-azure-rbac#assign-roles-for-the-fhir-service) to configure roles.
	
	- Follow  steps to execute the SearchParameter script:

		1. You can run the Powershell script locally, or you can use [Open Azure Cloud Shell](https://shell.azure.com) - you can also access this from [Azure Portal](https://portal.azure.com).\
		More details on how to setup [Azure Cloud Shell](https://learn.microsoft.com/azure/cloud-shell/overview)

			- If using Azure Cloud Shell, select PowerShell for the environment 
			- Clone this repo
				```azurecli
				git clone https://github.com/Azure-Samples/azure-health-data-and-ai-samples.git --depth 1
				```
			- Change working directory to the repo directory
				```azurecli-interactive
				cd $HOME/azure-health-data-and-ai-samples/lift-shift
				```
		2. Sign into your Azure account
			``` PowerShell
			az account set -s 'xxxx-xxxx-xxxx-xxxx-xxxxxx'
			```
			where 'xxxx-xxxx-xxxx-xxxx-xxxxxx' is your subscription ID.

		3. Browse to the scripts folder under this path (..\script).

		4. Run the following PowerShell script. 
			```Powershell
			./SearchParameters.ps1 -srcurl '<Source FHIR server URL>' -desturl '<Destination FHIR server URL>' 
			```
			
			|Parameter   | Description   |
			|---|---|
			| srcurl | Source FHIR server URL.
            | desturl | Destination FHIR server URL.

			Example:
			``` PowerShell
			./SearchParameters.ps1  -srcurl 'https://<<SOURCE FHIR Server Name>>.fhir.azurehealthcareapis.com/' -desturl 'https://<<Destination FHIR server Name>>.fhir.azurehealthcareapis.com'
			```

## Data Movement Verification

If you'd like to verify that all of your custom search parameter was successfully imported into the new FHIR server, follow these steps. This verification will only work if the destination Azure Health Data Services FHIR service was initially doesn't have the custom search parameter resource. 

- Get the custom search parameter FHIR resource count(s) on source Azure API for FHIR service.  
	```PowerShell
	GET https://<<FHIR_SERVICE_NAME>>.azurehealthcareapis.com/SearchParameter?_summary=count
	```
- Now run the below command to check the resource count on destination Azure Health Data Services FHIR service.  
	```PowerShell
	GET https://<<WORKSPACE_NAME>>-<<FHIR_SERVICE_NAME>>.fhir.azurehealthcareapis.com/SearchParameter?_summary=count
	```
- Compare the count(s) with exported FHIR resource count(s) to make sure that they match.

**NOTE** : Destination AHDS FHIR Service should not be used by any other applications or users until the script completes the process, as it will lead to miscount of the resources. 
