# Analytics pipeline visualization: PowerBI dashboard from parquet files in Azure Data Lake sample

This sample will focus on how to visualize FHIR data that **has already been converted to parquet files in Azure Data Lake**. This sample creates a PowerBI dashboard from parquet files in an Azure Data Lake storage account via a Synapse workspace.

If you have FHIR data that needs to be converted into parquet files in Azure Data Lake, please refer to our documentation on the [OSS FHIR to Synapse Sync Agent](https://github.com/microsoft/FHIR-Analytics-Pipelines/blob/main/FhirToDataLake/docs/Deploy-FhirToDatalake.md) tool. 

If you want to follow this tutorial from scratch with provided sample data, instructions will be provided as well. 

## End-to-end pipeline
The end-to-end pipeline is shown in the diagram below, starting with FHIR data in a FHIR server. 
![End to end pipeline](https://github.com/Azure-Samples/azure-health-data-services-samples/blob/snarang/powerbianalytics/samples/Analytics%20Visualization/docs/analyticspipelinediagram.png)
- In Stage 1: Convert FHIR data to Parquet, FHIR data in a FHIR server is converted to Parquet files (to help facilitate easier data analysis) and stored in Data Lake. This is done with our [OSS FHIR to Synapse Sync Agent](https://github.com/microsoft/FHIR-Analytics-Pipelines/blob/main/FhirToDataLake/docs/Deploy-FhirToDatalake.md) tool, or our analytics connector private preview. 
- In Stage 2: Create external tables, external tables and views of that Parquet files are made in Synapse.
- In Stage 3: Query and Visualize, Stored Procedures query the data to visualize in a PowerBI dashboard. This sample mainly focuses on Stage 3. Stage 1 and 2 are covered more in-depth separately (references will be provided for these steps).

# Stage 1: Convert FHIR data to Parquet
First, convert your FHIR data into Parquet files and store them in Azure Data Lake. Converting FHIR data into Parquet files makes it easier to facilitate data analysis later on.
## Option 1: Using your own sample data + FHIR to Synapse Sync Agent or analytics connector private preview
- If you have your own sample FHIR data that needs to be converted into Parquet files, please use our OSS tool [FHIR to Synapse Sync Agent](https://github.com/microsoft/FHIR-Analytics-Pipelines/blob/main/FhirToDataLake/docs/Deploy-FhirToDatalake.md) and follow steps 1 - 3. Once you are finished, move on to "Stage 2: Create external tables and views". 
- If you have already converted your FHIR data into Parquet files with our [FHIR to Synapse Sync Agent OSS tool](https://github.com/microsoft/FHIR-Analytics-Pipelines/blob/main/FhirToDataLake/docs/Deploy-FhirToDatalake.md), or you are coming from our analytics connector private preview, please move on to "Stage 2: Create external tables and views".

## Option 2: Using provided sample data
If you do not have your own sample FHIR data, or you would like to use our provided sample data parquet files, below steps will create a Data Lake and copy our sample Parquet files inside. Please note that this copies over sample Parquet files into Data Lake and is only used to quickly deploy this sample.

Follow steps in the "Stage 1" section [here](https://github.com/Azure-Samples/azure-health-data-services-samples/edit/snarang/powerbianalytics/samples/Analytics%20Visualization/docs/How%20to%20use%20provided%20sample%20data.md#stage-1-convert-fhir-data-to-parquet) #TODO FIX LINK

# Stage 2: Create external tables and views
Next, create external tables and views from the Parquet files. 
## Option 1: Using your own sample data + FHIR to Synapse Sync Agent or analytics connector private preview
If you have already used the [FHIR to Synapse Sync Agent OSS tool](https://github.com/microsoft/FHIR-Analytics-Pipelines/blob/main/FhirToDataLake/docs/Deploy-FhirToDatalake.md) or our analytics connector private preview to convert FHIR data to Parquet files, please follow steps 4 - 7 [here](https://github.com/microsoft/FHIR-Analytics-Pipelines/blob/main/FhirToDataLake/docs/Deploy-FhirToDatalake.md) to create the external tables and views. Note that if you do not already have a Synapse workspace, you will need to create a Synapse workspace in Azure Portal before proceeding. Once you have completed those steps, please move on to "Stage 3: Query and Visualize".

## Option 2: Using provided sample data 
If you are using provided Parquet sample files to run this sample, please follow steps in "Stage 2" section [here](https://github.com/Azure-Samples/azure-health-data-services-samples/edit/snarang/powerbianalytics/samples/Analytics%20Visualization/docs/How%20to%20use%20provided%20sample%20data.md#stage-2-create-external-tables-and-views) #TODO FIX LINK



# Stage 3: Query and Visualize
Finally, create SQL stored procedures to query the external tables, and visualize that data in PowerBI. This example PowerBI visualizes the percentage of women 50-70 years of age who had a mammogram to screen for breast cancer in the 48 months prior to the end of the measurement period. 
Note: This is a simple, basic example to demonstrate capabilities of FHIR analytics Pipeline and Data Visualization with Power BI, and should not be used for HEDIS quality measures. This sample uses Synthea data comprising of SNOMED codes.

### Prerequisites needed
1.	Microsoft work or school account
2.	Azure Synapse Workspace with Serverless SQL Endpoint.
	-	The Serverless SQL Endpoint will be used to connect to Database from Power BI Desktop Application to create Power BI Dashboard/reports.
3.	Power BI Desktop application
	-	It is available for download [here](https://www.microsoft.com/en-us/download/details.aspx?id=58494).
4.	Power BI service account
	-	Login to power BI service [here](https://msit.powerbi.com/home) and create a workspace where Power Bi reports will be published from Power Bi Desktop App
5.	Microsoft SQL Server Management Studio
	-	It is available for Download [here](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms?view=sql-server-ver16).

## Query: Setting up Database from SQL Server Management Studio

### Option 1: Using your own sample data + FHIR to Synapse Sync Agent or analytics connector private preview
 If you used the [FHIR to Synapse Sync Agent OSS tool](https://github.com/microsoft/FHIR-Analytics-Pipelines/blob/main/FhirToDataLake/docs/Deploy-FhirToDatalake.md) or our analytics connector private preview to convert FHIR data to Parquet files, please follow these steps to upload the stored procedures for querying.

1.	Find the “sp_getBCSComplianceDetails.sql” file in this repo (..azure-health-data-services-samples/samples/Analytics Visualization/scripts/sql/Stored_Procedure)

2.	Open  “Microsoft SQL Server Management Studio”, and connect to your database server using "Serverless SQL endpoint".  
The Serverless SQL endpoint can be found in Synapse Workspace as highlighted below:

![image](https://user-images.githubusercontent.com/116351573/209016888-8836d4e6-59bd-4eac-80a8-920233c13345.png)  

Open the “Microsoft SQL Server Management Studio”, from “Object Explorer” menu click on “Connect” and select “Database Engine” from list.

![image](https://user-images.githubusercontent.com/116351573/209016930-88397ea6-1972-457a-9ca1-7317fc9532a7.png)  

Enter serverless SQL endpoint from Synapse workspace into server name textbox, choose authentication method, provide username, and click connect:

![image](https://user-images.githubusercontent.com/116351573/209016965-06591ed5-8a61-4716-b143-1c12433ee3e3.png)  

After clicking connect you will be asked to provide authentication details for user, once user authentication is done, database connection will be done:

![image](https://user-images.githubusercontent.com/116351573/209017010-390892c6-4063-455b-a6fd-ceea7c714b76.png)  

In below sample, the database is called “fhirdb”

![image](https://user-images.githubusercontent.com/116351573/209015656-67b40cb3-b343-4b2a-b54a-ffa27d7cdd16.png)

3.	Click “File” menu choose “Open” and select “File” option, browse to the location of the “sp_getBCSComplianceDetails.sql” file from step 1:

![image](https://user-images.githubusercontent.com/116351573/209015736-dc345b74-bc87-4d59-960a-33d6f8a41abf.png)

4.	Select the database where you wish to create stored procedure, update the database name in script and click “Execute”.

![image](https://user-images.githubusercontent.com/116351573/209015945-75df4542-3fab-4275-8079-174346da42fd.png)

5.	Verify the output for any success and check the stored procedure created under “Stored Procedures” folder in database:

![image](https://user-images.githubusercontent.com/116351573/209016007-05d2f17d-0209-48df-99cc-d7d2778c6d8c.png)

6.	Proceed to “Visualize: Checking and editing the dashboard in Power BI desktop application” section.

###  Option 2: Using provided sample data 
If you are using provided Parquet sample files to run this sample, the stored procedure was already created from the Bicep template and is available in the database.
To explore (view/edit) the stored procedure used for Power BI dashboard report, we need to connect to database using “Serverless SQL endpoint” in synapse workspace that was created by BICEP.

The Serverless SQL endpoint can be found in Synapse Workspace as highlighted below:

![image](https://user-images.githubusercontent.com/116351573/209016888-8836d4e6-59bd-4eac-80a8-920233c13345.png)

1.	Open the “Microsoft SQL Server Management Studio”, from “Object Explorer” menu click on “Connect” and select “Database Engine” from list.

![image](https://user-images.githubusercontent.com/116351573/209016930-88397ea6-1972-457a-9ca1-7317fc9532a7.png)

2.	Enter serverless SQL endpoint from Synapse workspace into server name textbox, choose authentication method, provide username, and click connect:

![image](https://user-images.githubusercontent.com/116351573/209016965-06591ed5-8a61-4716-b143-1c12433ee3e3.png)

3.	After clicking connect you will be asked to provide authentication details for user, once user authentication is done, database connection will be done:

![image](https://user-images.githubusercontent.com/116351573/209017010-390892c6-4063-455b-a6fd-ceea7c714b76.png)

4.	Once the connection is made you would be able to see “fhirdb” database, stored procedures should be available inside “Programmability => Stored Procedures” folder as shown below:

![image](https://user-images.githubusercontent.com/116351573/209017036-69925a00-8050-4989-a5f9-3f67134a93bb.png)

5. Proceed to “Visualize: Checking and editing the dashboard in Power BI desktop application” section.

## Visualize: Checking and editing the dashboard in Power BI desktop application

Before proceeding ahead with dashboard, please ensure that the previous section "Query: Setting up Database from SQL Server Management Studio" has been completed, you are connected to “fhirdb” database using serverless SQL endpoint from Microsoft SQL Server Management Studio, and that the stored procedures are there.
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

5.	If there is an “Edit permission” button, click on it, if there is “Edit Credentials” button, jump to step 8.

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


## View the dashboard in Power BI service

1.	Login to Power BI service and select your workspace in left-hand side pane, under workspaces scroll down and select your report from “Reports” section, once you select the report, it will open, and you can see all the pages listed in “Pages” as highlighted in below image:

![image](https://user-images.githubusercontent.com/116351573/209018245-aff71eed-8250-4a76-8ddc-b7ae2d1bd91c.png)

2.	The arrow sign button at top right corner of each graph in main dashboard will navigate to detailed page as shown below:

![image](https://user-images.githubusercontent.com/116351573/209018902-608c23e7-f321-4d0d-967c-d16e63ccb2fd.png)

3.	Each detailed page has a navigation button which navigates back to Main Dashboard page as shown below:

![image](https://user-images.githubusercontent.com/116351573/209018922-90e4ce66-cf61-47c8-98d5-3e6282628f78.png)







