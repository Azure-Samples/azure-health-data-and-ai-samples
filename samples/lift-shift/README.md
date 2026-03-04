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

The PowerShell script will migrate custom search parameters from the Azure API for FHIR server to Azure Health Data Services FHIR service and automatically trigger a reindex operation to make them usable.

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
## Script Execution Process

The script performs the following operations in sequence:

### 1. Fetch Custom Search Parameters
The script connects to the source Azure API for FHIR server and retrieves all custom search parameters (excluding standard HL7 search parameters).

**Expected Messages:**
- `"Fetching SearchParameters from Source FHIR Service..."` 
- `"Custom SearchParameters found: <count>"` 
- `"No custom SearchParameters found."`  if no custom parameters exist

### 2. Post Search Parameters
The retrieved custom search parameters are posted to the destination Azure Health Data Services FHIR service.

**Expected Messages:**
- `"Posting SearchParameters to Destination FHIR Service..."` 
- `"Search parameters posted successfully."` 

### 3. Initiate Reindex Operation
After successfully posting the search parameters, the script automatically triggers a reindex operation on the destination FHIR service. This step is **critical** as custom search parameters must be indexed before they can be used in queries.

**Expected Messages:**
- `"Starting reindex operation..."` 
- `"Reindex operation initiated successfully."` 

### 4. Monitor Reindex Status
The script continuously monitors the reindex operation status until completion or timeout.

**Expected Messages:**
- `"Polling reindex status at <status_url>"` 
- `"Maximum wait time: 15 minutes"` 
- `"Reindex status (attempt <number>, <remaining_minutes> min remaining): <status>"` 
  - Possible status values: `running`, `queued`, `completed`, `failed`, `cancelled`
- `"Reindex operation completed successfully!"` - when reindex completes

**Timeout Handling:**

If the reindex operation exceeds the 15-minute wait time, you will see:
- `"Reindex operation timeout reached after 15 minutes."` 
- `"The reindex operation is still running but has exceeded the script wait time."` 
- `"To check the status manually, use the following URL:"`
- `<status_url>` 
- `"You can monitor the reindex status by making a GET request to the above URL."` 
- `"The script will now exit. Please check the reindex status manually."` 

**Note:** The timeout does not mean the reindex failed - it only means the script's monitoring period has ended. The reindex operation continues running in the background. You can manually check its status using the provided URL.

## Important Notes

1. **Reindex Duration**: The reindex operation duration depends on the size of your FHIR database and the number of custom search parameters. For large databases, it may take longer than 15 minutes.

2. **Manual Status Check**: If the script times out, you can check the reindex status manually by making a GET request to the status URL provided in the console output using any REST client or browser (with appropriate authentication).

3. **Search Parameter Availability**: Custom search parameters will **not** be usable for queries until the reindex operation completes successfully. Always verify that the reindex has completed before attempting to use the new search parameters.

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

**IMPORTANT NOTES:** 
- The script only migrates **custom search parameters** (those not prefixed with `http://hl7.org`). Built-in HL7 standard search parameters are not migrated as they already exist in the destination FHIR service.
- The total `SearchParameter` resource count on the destination server will be higher than the migrated count because it includes built-in search parameters.
- Destination AHDS FHIR Service should not be used by any other applications or users until the script completes the migration and reindex process, as concurrent operations may affect resource counts and search parameter availability.
- Verify that the reindex operation has completed successfully before using the newly migrated custom search parameters in queries.
