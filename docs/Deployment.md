# Analytics pipeline visualization: PowerBI dashboard from parquet files in Azure Data Lake sample

This sample will demonstrate how to visualize FHIR data that has already been converted to parquet files in Azure Data Lake. This sample creates a PowerBI dashboard from parquet files in an Azure Data Lake storage account via a Synapse workspace.

If you have FHIR data that needs to be converted into parquet files in Azure Data Lake, please refer to our documentation on the [OSS FHIR to Synapse Sync Agent](https://github.com/microsoft/FHIR-Analytics-Pipelines/blob/main/FhirToDataLake/docs/Deploy-FhirToDatalake.md) tool. 

This sample also contains a script to create External Tables, Views and Stored Procedure in Synapse Serverless SQL pool pointing to the parquet files. The sample code provided enables you to query against the entire FHIR data with tools such as Synapse Studio, SSMS, and Power BI. You can also access the parquet files directly from a Synapse Spark pool.

## Deployment

### Prerequisites

- An Azure Account to create Data lake and Synapse workspace

### Steps at high level

1. Deploy the DataLake Storage and Synapse workspace using the given biceps template.
2. Provide access of the Storage Account and the Synapse workspace to your account for running the PowerScript mentioned below.
3. Provide access of the Storage Account to the Synapse Workspace to access the data from Synapse.
4. Run the provided PowerShell script that creates following artifacts:
    1. Resource specific folders in the Azure Storage Account.
    1. A database in Synapse serverless pool with External Tables ,Views and Stored Procedure pointing to the files in the Storage Account.
5. Verify that the data gets copied to the Storage Account. If data is copied to the Storage Account.
6. Query data from Synapse Studio.

### 1. Deploy the Sample
1. Deploy the Bicep template.

    It will create the data lake storage account and synapse workspace in the new resource group or in the exsiting resource group as per the paramter configuration.

    1. Install Powershell [Az](https://docs.microsoft.com/en-us/powershell/azure/install-az-ps?view=azps-7.1.0). Clone the repo and browse to the infra folder under this path (..\FhirToDataLake\scripts\infra). Log in by running az login and following the instructions.
    2. Run below command on powershell terminal to set the subscription where the infra deployment will be done.
    ```Powershell
    az account set -s 'xxxx-xxxx-xxxx-xxxx-xxxxxx'
    ```
    where 'xxxx-xxxx-xxxx-xxxx-xxxxxx' is your subscription ID.
    
    3. Browse to the main.paramter.json under this path (..\scripts\infra).
    4. Set the paramter values as per your requirement. For more details, refer to the below :

        |Parameter   | Description   |
        |---|---|
        | Name | Name of the environment which is used to generate a short unique hash used in resources. |
        | DataLakeName | Name of Data Lake Storage Account where parquet files are stored. |
        | DataLakefileSystemName | Name of container (filesystem) required while creating synapse workspace. |
        | SynapseworkspaceName | Name of Synapse Workspace. |
        | SqlAdministratorLogin | SQL server admin required while creating synpase workspace. |
        | SqlAdministratorLoginPassword | Password for SQL server. This is a secure parameter. |
        | Location | Location where the resources will be deployed. |
        | AllowAllConnections | Allow all connection for synapse workspace firewall. |
        | ExistingResourceGroupName | Name of your existing resource group (leave blank to create a new one). |

	5. Then run below bicep deployment command on powershell terminal.
        ```Powershell
        az deployment sub create --name demoSubDeployment --location eastus --template-file main.bicep --parameters main.parameters.json
        ```
2. Provide privilege to your account
You must provide the following roles to your account to run the PowerShell script in the next step. You may revoke these roles after the installation is complete.

    1. In your Synapse workspace, select Synapse Studio > Manage > Access Control, and then provide the Synapse Administrator role to your account.
    2. In the Storage Account created during the pipeline installation, select the Access Control (IAM) and assign the Storage Blob Data Contributor role to your account.

3. Provide access of the Storage Account to the Synapse Workspace
To enable Synapse to read the data from the Storage Account, assign the Storage Blob Data Contributor role to it. You can do this by selecting Managed identify while adding members to the role. You should be able to pick your Synapse workspace instance from the list of managed identities shown on the portal.

4. Run the PowerShell script.

    Running the PowerShell script that creates following artifacts:

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
    5. Sign in to your Azure account to the subscription where synapse is located.
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

5. Download the [AzCopy](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-v10)

    1. Go to the dowloaded folder of azcopy.
    2. Login to Azcopy with tenant id using below command
        ```Powershell
        azcopy login --tenant-id 'xxxx-xxxx-xxxx-xxxx'  
        ```
    3. Run below command to copy the parquet file to the data lake storage which is created while running the bicep template

        ```Powershell
        azcopy copy 'https://ahdssampledata.blob.core.windows.net/fhir/50k/fhir/result' 'https://{DestinationstorageAccountName}.blob.core.windows.net/fhir' --recursive
        ```

6. Go to your Synapse workspace serverless SQL pool. You should see a new database named fhirdb. Expand External Tables and Views to see the entities. Your FHIR data is now ready to be queried.
