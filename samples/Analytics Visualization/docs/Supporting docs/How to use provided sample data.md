# How to use provided sample data

If you do not have your own sample FHIR data, or you would like to use our provided sample data parquet files, below steps will create a Data Lake and copy our sample Parquet files inside.

Please note that this copies over sample Parquet files into Data Lake and is only used to quickly deploy this sample. If you have yout own sample FHIR data that needs to be analyzed, please follow the above steps in Option 1 to use the [FHIR to Synapse Sync Agent OSS tool](https://github.com/microsoft/FHIR-Analytics-Pipelines/blob/main/FhirToDataLake/docs/Deploy-FhirToDatalake.md) to convert the FHIR data into Parquet files. 

# Stage 1: Convert FHIR data to Parquet
### Prerequisites needed
- An Azure Account to create Data lake and Synapse workspace
### Steps
1. Deploy the Bicep template by following these steps.

    It will create a Data Lake storage account and Synapse workspace in the new resource group or in the exsiting resource group as per the parameter configuration.

    1. Install Powershell [Az](https://docs.microsoft.com/en-us/powershell/azure/install-az-ps?view=azps-7.1.0). Install [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli). Clone the repo and browse to the infra folder under this path (..\azure-health-data-services-samples\scripts\infra). Log in by running "az login" and following the instructions.
    2. Run below command on powershell terminal to set the subscription where the infra deployment will be done.
    ```PowerShell
    az account set -s 'xxxx-xxxx-xxxx-xxxx-xxxxxx'
    ```
    where 'xxxx-xxxx-xxxx-xxxx-xxxxxx' is your subscription ID.
    
    3. Browse to the main.parameters.json under this path (..\scripts\infra).
    4. Set the parameter values as per your requirement. For more details, refer to the below :

        |Parameter   | Description   |
        |---|---|
        | Name | Name of the environment which is used to generate a short unique hash used in resources. |
        | DataLakeName | Provide a name for the Data Lake Storage Account where parquet files will be stored. |
        | DataLakefileSystemName | Provide a name for the container (filesystem) while creating synapse workspace. |
        | SynapseworkspaceName | Provide name for the Synapse Workspace. |
        | SqlAdministratorLogin | SQL server admin required while creating synpase workspace. |
        | Location | Location where the resources will be deployed. |
        | AllowAllConnections | Allow all connection for synapse workspace firewall. |
        | ExistingResourceGroupName | Name of your existing resource group (leave blank to create a new one). |

	5. Then run below bicep deployment command on powershell terminal.
        ```Powershell
        az deployment sub create --name demoSubDeployment --location eastus --template-file main.bicep --parameters main.parameters.json
        ```
2. Provide privilege to your account.  
You must provide the following roles to your account to run the PowerShell script in the next step. You may revoke these roles after the installation is complete.

    1. In your Synapse workspace, select Synapse Studio > Manage > Access Control, and then provide the Synapse Administrator role to your account.
    2. In the Storage Account created during the pipeline installation, select the Access Control (IAM) and assign the Storage Blob Data Contributor role to your account.

3. Move on to "Stage 2: Create external tables and views"


# Stage 2: Create external tables and views
1. Provide access of the Storage Account to the Synapse Workspace.  
To enable Synapse to read the data from the Storage Account, assign the Storage Blob Data Contributor role to it. In your Storage Account created during the pipeline installation, select the Access Control (IAM), assign Store Blob Data Contributor, and select Managed Identity while adding members to the role. You should be able to pick your Synapse workspace instance from the list of managed identities shown on the portal.

2. Run the PowerShell script.

    Running the PowerShell script creates the following artifacts:

    1. Resource specific folders in the Azure Storage Account.
    2. A database in Synapse serverless SQL pool with External Tables, Views and Store Procedure pointing to the files in the Storage Account.

    To run the PowerShell Script, perform the following steps:

    1. Clone this [FHIR-Analytics-Power-BI](https://github.com/Azure-Samples/azure-health-data-services-samples) repo to your local machine.
    2. Open the PowerShell console, ensure that you have the latest version of the **PowerShell 7** or **Powershell 5.1**.
    3. Install Powershell [Az](https://docs.microsoft.com/en-us/powershell/azure/install-az-ps?view=azps-7.1.0) and separated [Az.Synapse](https://docs.microsoft.com/en-us/cli/azure/synapse?view=azure-cli-latest) modules if they don't exist.
    ``` PowerShell
    Install-Module -Name Az
    Install-Module -Name Az.Synapse
    ```
    4. Install Powershell [SqlServer](https://learn.microsoft.com/en-us/sql/powershell/download-sql-server-ps-module?view=sql-server-ver16) module if it doesn't exist.
    ``` PowerShell
    Install-Module -Name SqlServer
    ```
    5. Sign in to your Azure account to the subscription where synapse is located. where 'yyyy-yyyy-yyyy-yyyy' is your subscription ID.
    ``` PowerShell
    Connect-AzAccount -SubscriptionId 'yyyy-yyyy-yyyy-yyyy'
    ```
    6. Browse to the scripts folder under this path (..\scripts).
    7. Run the following PowerShell script. 
    ```Powershell
    ./Set-SynapseEnvironment.ps1 -SynapseWorkspaceName "{Name of your Synapse workspace instance}" -StorageName "{Name of your storage account where Parquet files will be written}".
    ```
    For more details, refer to the complete syntax below.
    ``` PowerShell
    Set-SynapseEnvironment
        [-SynapseWorkspaceName] <string>
        [-StorageName] <string>
        [[-Database] <string>, default: “fhirdb”]
        [[-Container] <string>, default: “fhir”]
        [[-ResultPath] <string>, default: “result”]
        [[-MasterKey] <string>, default: ”FhirSynapseLink0!”]
        [[-Concurrent] <int>, default: 15]
        [[-CustomizedSchemaImage] <string>, default: None]
    ```

    |Parameter   | Description   |
    |---|---|
    | SynapseWorkspaceName | Name of Synapse workspace instance. |
    | StorageName | Name of Storage Account where parquet files are stored. |
    | Database | Name of database to be created on Synapse serverless SQL pool |
    | Container | Name of container on storage where parquet files are stored. |
    | ResultPath | Path to the parquet folder. |
    | MasterKey | Master key that will be set in the created database. The database needs to have the master key, and then you can create EXTERNAL TABLEs and VIEWs on it. |
    | Concurrent | Max concurrent tasks number that will be used to upload place holder files and execute SQL scripts. |
    | CustomizedSchemaImage | Customized schema image reference. Need to be provided when customized schema is enable. |

3. Download [AzCopy](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-v10) if you don't already have it.

    1. Go to the downloaded folder of azcopy.
    2. Login to Azcopy with tenant id using below command
        ```Powershell
        azcopy login --tenant-id 'xxxx-xxxx-xxxx-xxxx'  
        ```
    3. Run below command to copy the sample Parquet files to your new Data Lake storage which was created while running the above bicep template

        ```Powershell
        azcopy copy 'https://ahdssampledata.blob.core.windows.net/fhir/50k/fhir/result' 'https://{DestinationstorageAccountName}.blob.core.windows.net/fhir' --recursive
        ```

4. Go to your Synapse workspace serverless SQL pool. You should see a new database named fhirdb. Expand External Tables and Views to see the entities. Your FHIR data is now ready to be queried.
5. Move on to "Stage 3: Query and Visualize" [here](https://github.com/Azure-Samples/azure-health-data-services-samples/blob/snarang/powerbianalytics/samples/Analytics%20Visualization/docs/Deployment.md#stage-3-query-and-visualize) #TODO FIX LINK



