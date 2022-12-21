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

    1. Install Powershell [Az](https://docs.microsoft.com/en-us/powershell/azure/install-az-ps?view=azps-7.1.0). Install [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli). Clone the repo and browse to the infra folder under this path (..\azure-health-data-services-samples\scripts\infra). Log in by running az login and following the instructions.
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


# Data visualization with Power BI using FHIR analytics pipelines for Breast Cancer Screening (BCS)

With Power BI as Data visualization tool, this is an application to visualize FHIR Data using FHIR Analytics Pipeline. In pipeline new or updated FHIR data is moved to Azure Data Lake stores data in parquet files, from parquet files we create external tables and views in Azure Synapse Analytics database with Serverless SQL pool. On the Database we have stored procedures to calculate data as per quality measures, that stratified data is visualized on power BI desktop report.

## About FHIR to Data Lake pipeline:
The FHIR Analytics Pipeline is an Azure Function that continually exports new and modified FHIR resources in specific time chunks to Azure Data Lake Storage in the form of Parquet files. By default, this function runs every 5 minutes and exports changed data in five-minute chunks (or windows). Each export window will only export the latest version of a resource and a single resource will exist in multiple windows as it's changed in the FHIR service. 
![image](https://user-images.githubusercontent.com/116351573/209014574-ef46aa8c-f41e-400a-a79f-a1124cdf921d.png)
	Figure 1 - Sample Data Flow for FHIR analytics pipeline and Power BI sample application

## Database in Synapse:
A database is created in Synapse with serverless pool, database has External Tables and Views pointing to the parquet files in the Azure Data Lake Storage.
## About Power BI Dashboard:
The Power BI dashboard demonstrated in this document uses the sample quality measure below to get the data from Serverless SQL tables/views using stored procedures to show data visualization in power BI.
## Quality Measure: 
Percentage of women 50-70 years of age who had a mammogram to screen for breast cancer in the 48 months prior to the end of the measurement period. 
Note: This sample is not the same as the HEDIS eCQI digital quality measure. This is a basic sample to demonstrate capabilities of FHIR analytics Pipeline and Data Visualization with Power BI. This sample uses Synthea data comprising of SNOMED codes.

### Below is the prerequisite that we need for this application:
1.	Microsoft work or school account
2.	Azure Synapse Workspace with Serverless SQL Endpoint.
	-	The Serverless SQL Endpoint will be used to connect to Database from Power BI Desktop Application to create Power BI Dashboard/reports.
3.	Power BI Desktop application
	-	It is available for download [here](https://www.microsoft.com/en-us/download/details.aspx?id=58494).
4.	Power BI service account
	-	Login to power BI service [here](https://msit.powerbi.com/home) and create a workspace where Power Bi reports will be published from Power Bi Desktop App
5.	Microsoft SQL Server Management Studio
	-	It is available for Download [here](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms?view=sql-server-ver16).

## Setting up Database from SQL Server Management Studio

### For customers who have not used BICEP Template to create FHIR Analytics pipeline:
If you have not set up FHIR Analytics pipeline with sample data using BICEP template just want to visualize the data you already have in your database using power bi report, follow below steps to create the stored procedure to be used by power bi report:
1.	Check “sp_getBCSComplianceDetails.sql” file (..azure-health-data-services-samples/scripts/sql/Stored_Procedure)

2.	Open the “Microsoft SQL Server Management Studio”, Connect to your database server. In below sample database is “fhirdb”

![image](https://user-images.githubusercontent.com/116351573/209015656-67b40cb3-b343-4b2a-b54a-ffa27d7cdd16.png)

3.	Click “File” menu choose “Open” and select “File” option, browse to the location of sql file from step 1:

![image](https://user-images.githubusercontent.com/116351573/209015736-dc345b74-bc87-4d59-960a-33d6f8a41abf.png)

4.	Select the database where you wish to create stored procedure, update the database name in script and click “Execute”.

![image](https://user-images.githubusercontent.com/116351573/209015945-75df4542-3fab-4275-8079-174346da42fd.png)

5.	Verify the output for any success and check the stored procedure created under “Stored Procedures” folder in database:

![image](https://user-images.githubusercontent.com/116351573/209016007-05d2f17d-0209-48df-99cc-d7d2778c6d8c.png)

6.	Go to “Checking and editing the dashboard in Power BI desktop application” section.

### For customers who used BICEP Template to create FHIR Analytics pipeline
If you have created FHIR analytics pipeline using the BICEP template, Stored procedure would be created and available in the database.
To explore (view/edit) the stored procedure used for Power BI dashboard report, we need to connect to database using “Serverless SQL endpoint” in synapse workspace that was created by BICEP.

Serverless SQL endpoint could be found in Synapse Workspace as highlighted below:

![image](https://user-images.githubusercontent.com/116351573/209016888-8836d4e6-59bd-4eac-80a8-920233c13345.png)

1.	Open the “Microsoft SQL Server Management Studio”, from “Object Explorer” menu click on “Connect” and select “Database Engine” from list.

![image](https://user-images.githubusercontent.com/116351573/209016930-88397ea6-1972-457a-9ca1-7317fc9532a7.png)

2.	Enter serverless SQL endpoint from Synapse workspace into server name textbox, choose authentication method, provide username, and click connect:

![image](https://user-images.githubusercontent.com/116351573/209016965-06591ed5-8a61-4716-b143-1c12433ee3e3.png)

3.	After clicking connect you will be asked to provide authentication details for user, once user authentication is done, database connection will be done:

![image](https://user-images.githubusercontent.com/116351573/209017010-390892c6-4063-455b-a6fd-ceea7c714b76.png)

4.	Once the connection is made you would be able to see “fhirdb” database, stored procedures should be available inside “Programmability => Stored Procedures” folder as shown below:

![image](https://user-images.githubusercontent.com/116351573/209017036-69925a00-8050-4989-a5f9-3f67134a93bb.png)


## Checking and editing the dashboard in Power BI desktop application

Before proceeding ahead with dashboard, please connect to “fhirdb” database using serverless SQL endpoint from Microsoft SQL Server Management Studio and make sure the stored procedures are created there.
1.	Check “BCS_Compliance_Dashboard.pbix” file at (../azure-health-data-services-samples/powerbianalytics/powerbiReport).
![image](https://user-images.githubusercontent.com/116351573/209017082-b65e9b78-e414-4979-8a21-5285ed5c5cec.png)

2.	Double click on report file(.pbix) to open it in Power BI Desktop application, the report file has multiple pages, “Main Dashboard” is the landing page, as shown in below image:

![image](https://user-images.githubusercontent.com/116351573/209017128-2ed5f5c4-949b-42e0-9cb3-de36591025ab.png)

### The report has below pages:
1.	Main Dashboard - It’s an entry point for a report, it shows all the graphs at one place with navigation buttons to detailed pages.
2.	Compliance By Age - This page shows charts with Age range and Age wise compliance.
3.	Compliance By Place - This page shows the map and chart with City wise compliance.
4.	Compliance By Race Category - This page shows a chart with Race Category wise compliance.
5.	Compliance By Ethnicity Category - This page shows a chart with Ethnicity Category wise compliance.
6.	Compliance By Payor - This page shows a chart with Payor wise compliance.

### The Main Dashboard page in report shows all the visualizations listed below:
•	Age Range Compliance Graph

•	Place wise Compliance Map

•	Compliance by Race Category

•	Compliance by Ethnicity Category

•	Payor wise Compliance

•	Cards with details of Total Patients, Patients with Mammogram Procedure and Compliance %

#### All the charts get data from “ComplianceData” table.

3.	Below image shows the “Data” section of the report which has tables, on selecting specific table in the right-hand side pane, it will show the data in that table as shown in below image:

![image](https://user-images.githubusercontent.com/116351573/209017423-4e71681f-a7a2-4d35-8c48-8a965bd97595.png)

4.	The “Model” section shows all the table and the relationships (if any) as shown in below image:

![image](https://user-images.githubusercontent.com/116351573/209017445-3ddc654e-8340-46db-806b-1319e3b98a2d.png)


## Editing the query to change measurement period date range in parameters

1.	Go to “Model” section, here one can see the table is available, and can view properties and fields of the table, when you click “More Options” button (Three dots in top right corner of the table  ![image](https://user-images.githubusercontent.com/116351573/209017528-05921e0d-4ca9-493b-b520-d09325e01e39.png)
), it shows all the options available for the table, click on “Edit Query”.

![image](https://user-images.githubusercontent.com/116351573/209017551-5221c58e-d74c-4421-9261-09ca4779134d.png)

2.	A new “Power Query Editor” window will open, click on expand arrow button as highlighted in below image which will show the query:

![image](https://user-images.githubusercontent.com/116351573/209017584-1aec6844-5840-4bdf-8f11-a2e7734f78a3.png)

3.	If this is first time you are editing query in .pbix file, you will have to change the serverless SQL pool URL (blacked out part) in below image, you will be asked to edit credentials to connect to your SQL serverless pool URL, you will see “Edit Credentials” button, click on it and login using Microsoft account as shown in step 8, If query already has your SQL serverless URL then no need to change it, just change parameter values as shown in below step.

4.	Change the date value in query for measurement period start date and end date, first value is for start date and second value is for end date parameter respectively, so we have set 11/14/2010 as measurement period start date and 11/17/2022 as measurement period end date:
![image](https://user-images.githubusercontent.com/116351573/209017636-afab257e-9d19-4dab-84e2-c523c3219151.png)

After editing the values, click on right tick as highlighted in image below:
![image](https://user-images.githubusercontent.com/116351573/209017679-fd794acf-222b-4c39-bbaa-6cea2b6d47af.png)

5.	If there is an “Edit permission” button, click on it, if there is “Edit Credentials” button, jump to step 7.

![image](https://user-images.githubusercontent.com/116351573/209017715-5ebb2883-4aeb-4b77-8e39-3e63f6639dc0.png)
6.	On new pop-up window click on “Run” button:

![image](https://user-images.githubusercontent.com/116351573/209017747-ebe84dcf-edea-486b-81ef-1bb46951c4e4.png)
7.	Query will be executed, latest result data will be loaded into the table, this will complete query editing, close the “Power Query Editor” window:

![image](https://user-images.githubusercontent.com/116351573/209017776-f4b0728a-c0d9-4509-b648-49bb53febb82.png)

8.	If you see “Edit Credentials” button as shown below, click on it.

![image](https://user-images.githubusercontent.com/116351573/209017813-bf125d9f-fe46-484a-9919-efafdc38003a.png)

9.	A New popup window will appear, select Microsoft account from left-hand side pane and click sign in, sign in with Microsoft account.

![image](https://user-images.githubusercontent.com/116351573/209017839-96f17746-657d-40f1-8c9a-389e4b9c4fc5.png)

10.	After successfully signing in, click on “Connect”.

![image](https://user-images.githubusercontent.com/116351573/209017868-af215960-e699-4736-b5f3-ebe0bc0313de.png)


## Publish the dashboard in Power BI Service
To be able to view the dashboard in Power BI service we need to publish it from Power BI desktop application to Power BI Service.
1.	If any changes are made to dashboard, save the changes, and then click on “Publish” option from “Home” menu as shown in image below:

![image](https://user-images.githubusercontent.com/116351573/209017910-f66edecd-216e-43ce-b48e-cce2485c99b6.png)

2.	It will ask for the workspace in power BI service where you want to publish it, Select the specific workspace from dropdown list and click “Select”, in our case workspace name is “Quality Measure”, as shown in below:

![image](https://user-images.githubusercontent.com/116351573/209018024-9327ad8c-a790-4bb0-86ee-d694cab36278.png)

3.	If it is a fresh report, it will get published. In case you already have the report in workspace with same name, it will ask for confirmation to replace existing one. If you wish to replace the existing one click on replace, otherwise click cancel and save file with different name and follow steps again.

![image](https://user-images.githubusercontent.com/116351573/209018068-a2d48522-b1e6-4752-9646-821d0a2a290c.png)

4.	Once the report is published successfully, it will show pop-up with success message, click on “Got it” as below.

![image](https://user-images.githubusercontent.com/116351573/209018131-f08b4874-cf75-4585-af54-6106e6bb0727.png)


## View the dashboard in Power BI service!

1.	Login to Power BI service and select your workspace in left-hand side pane, under workspaces scroll down and select your report from “Reports” section, once you select the report, it will open, and you can see all the pages listed in “Pages” as highlighted in below image:

![image](https://user-images.githubusercontent.com/116351573/209018245-aff71eed-8250-4a76-8ddc-b7ae2d1bd91c.png)

2.	The arrow sign button at top right corner of each graph in main dashboard will navigate to detailed page as shown below:

![image](https://user-images.githubusercontent.com/116351573/209018902-608c23e7-f321-4d0d-967c-d16e63ccb2fd.png)

3.	Each detailed page has a navigation button which navigates back to Main Dashboard page as shown below:

![image](https://user-images.githubusercontent.com/116351573/209018922-90e4ce66-cf61-47c8-98d5-3e6282628f78.png)







