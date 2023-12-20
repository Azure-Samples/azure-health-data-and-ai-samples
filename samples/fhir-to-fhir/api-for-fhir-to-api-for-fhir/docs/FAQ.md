# FAQ's for FHIR-to-FHIR data movement

1. How do we perform FHIR to FHIR (Gen1 to Gen1) data transfer?
   + For implementation details and architecture overview please refer the [documentation](https://github.com/Azure-Samples/azure-health-data-services-samples/tree/main/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir#copying-data-from-one-azure-api-for-fhir-server-to-another-azure-api-for-fhir-server). 
     
     **Note**: This data transfer will only work if you have full access to create resources in your subscription and are not using private networking.

2. How to deploy FHIR loader?
   + Go to [FHIR Bulk Loader](https://github.com/microsoft/fhir-loader) and follow the [deployment steps](https://github.com/microsoft/fhir-loader#deployment).

3. How to configure Azure API for FHIR server for export?
   + Follow steps [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) to configure settings for export on Azure API for FHIR server.

4. Will there be any message to the user when export is finished?
   + No, users must check the status manually using the get request in Postman with URL returned in the Content-Location response headers of export operation response.
     
     If the URL returns status 202 Accepted, it means the export job is still in progress, if the status is 200 Completed, it means the export job is completed.
     
     More details could be found in the readme document of the app [here](https://github.com/Azure-Samples/azure-health-data-services-samples/tree/main/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir).

5. How do you check when the FHIR loader is done uploading?
   + Users must check the containers i.e., "bundles" and "ndjson" to know about FHIR loader completion. FHIR Loader has completed loading when these containers are empty.

6.	Do users need to wait for one export to finish, then do the import, wait for import to finish, then kick off the next export?
    + Within each migration, users need to wait for export to finish before they can start import so FHIR Loader can import everything that was exported (i.e., wait for export#1 to finish before starting import#1). However, users can start export#2 while import#1 is finishing up. Users can only overlap though by a factor of 1, and exports need to be going to different containers.

      Users should use _since and _till parameters with export.

      On initial export from the source FHIR server, they should include "_till" in $export

      After that initial migration, they should then run subsequent $export using "_since" and "_till".

7.	Are there any configurations to be done for Destination FHIR server and how to do them?
 	+ For Destination FHIR server we need to configure number of RU, auto scaling, number of nodes and throttling limits, Configuration details could be found in document [here](https://github.com/Azure-Samples/azure-health-data-services-samples/blob/main/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir/docs/Server_%26_App_Config.md#destination-fhir-server).

8.	What configurations to be considered for FHIR Bulk Loader Applications?
	+ Users can configure App Service plan, number of instances, number of fhir resources per bundle, more details can be found [here](https://github.com/Azure-Samples/azure-health-data-services-samples/blob/main/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir/docs/Server_%26_App_Config.md#fhir-bulk-loader-application).
    + To Enhance the Performance of **FHIR Loader**, details  can be found [here](https://github.com/microsoft/fhir-loader/blob/main/docs/performance.md).

9.	How do users verify the data movement?
	+ Users can follow [Data Movement Verification](https://github.com/Azure-Samples/azure-health-data-services-samples/tree/main/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir#data-movement-verification) steps to verify the data import.

10.	How do users troubleshoot any issue encountered during export?
	+ Users can check the troubleshooting details from [here](https://github.com/Azure-Samples/azure-health-data-services-samples/blob/main/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir/docs/Error_Handling.md#export-data).

11.	How do users check issues encountered with copy?
	+ Users can check for the possible reasons for issues with copy command from [here](https://github.com/Azure-Samples/azure-health-data-services-samples/blob/main/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir/docs/Error_Handling.md#copying-data).

12. How do users handle issues encountered during import by FHIR Loader?
	+ There are various errors which users could encounter for FHIR Loader while importing data, the details of troubleshooting FHIR loader issue are available [here](https://github.com/Azure-Samples/azure-health-data-services-samples/blob/main/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir/docs/Error_Handling.md#fhir-bulk-loader).

13.	Do we need two storage accounts for holding exported data and to be associated with FHIR loader?
    + No users don’t need two storage accounts, Users can use storage account that gets created with FHIR Loader installation, Export the data from source Azure API for FHIR to a new container in storage account linked to FHIR Loader.

14.	Can we run copy script from local machine OR it must be from Azure PowerShell?
	+ Yes, copy script can be run from local machine for that user should install the Az CLI and AZ copy locally and log into the azure account.
      
       **Note**: If the user is working with **private networks**, then above activity have to be done from an **execution environment** that can access the storage account(s). 
       
       **Recommendation**: Users should run the copy script from Azure PowerShell.

15.	How to rerun the failed files?
	+ Users have to rerun the failed files manually, more details are available [here](https://github.com/Azure-Samples/azure-health-data-services-samples/blob/main/samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir/docs/Error_Handling.md#fhir-bulk-loader).

16.	If we rerun the export, will it overwrite the files already exported?
    + No, it will not overwrite the already exported files. Each export will create a new folder/directory in the storage container and will export the data into it.

17.	Which azure region needs to be selected for the deployment?
    + All resources should be in the same region. Performance will degrade and egress costs will apply if this is not the case.

18.	How do I deploy this into an environment with "private networking"?
    
    To create environment with private network you need to disable public access of deployed azure resources and connect them within virtual network, you can follow below steps:
    1. Create virtual network, detail steps given [here](https://learn.microsoft.com/en-us/azure/virtual-network/quick-create-portal#create-a-virtual-network). 
    2. Once the Fhir Loader function app is deployed follow below steps:
       
       + Select **Networking**, then under [Access restriction](https://learn.microsoft.com/en-us/azure/app-service/overview-access-restrictions#app-access) unselect the check box and save.
       + To enable **VNet integration** for the function app, follow steps given [here](https://learn.microsoft.com/en-us/azure/azure-functions/functions-networking-options?tabs=azure-cli#enable-virtual-network-integration) (select the virtual network created in step i).
       + Creating Private endpoint for function app
          + Select **Networking** then under **Private endpoints**. 
          + Select **Add**, while creating private endpoint select the virtual network created in step i.

    3. Change **Public network access** for **Storage account**, which is deployed with Fhir loader function app 
       + Select **Networking** under **Security + networking**.
       + Under **Public network access**, select option **Enabled from selected virtual networks and IP addresses**, more details steps available [here](https://learn.microsoft.com/en-us/azure/storage/common/storage-network-security?tabs=azure-portal#change-the-default-network-access-rule).
       + Add existing virtual network created in step i.

    4. Enable Private endpoints for **Health Data Services workspace**
       + Select **Networking** under **Settings**.
       + Click **Private endpoint** to create new private endpoint.
       + Under **Allow access from** select option **Private endpoints**. 
       + Select the created private endpoint and approve the same.

















