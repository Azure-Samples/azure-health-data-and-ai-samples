# Appendix

## Uploading stored procedures for querying
Follow these steps to upload the stored procedures for querying.

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


## Connecting to Microsoft SQL Server Management Studio
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

## Navigating the ComplianceData table
3.	Below image shows the “Data” section of the report which has tables, on selecting specific table in the right-hand side pane, it will show the data in that table as shown in below image:

![image](https://user-images.githubusercontent.com/116351573/209017423-4e71681f-a7a2-4d35-8c48-8a965bd97595.png)

4.	The “Model” section shows all the table and the relationships (if any) as shown in below image:

![image](https://user-images.githubusercontent.com/116351573/209017445-3ddc654e-8340-46db-806b-1319e3b98a2d.png)

## Editing the query to change measurement period date range in parameters
1.	Go to “Model” section, here one can see the table is available, and can view properties and fields of the table, when you click “More Options” button (Three dots in top right corner of the table  ![image](https://user-images.githubusercontent.com/116351573/209017528-05921e0d-4ca9-493b-b520-d09325e01e39.png)
), it shows all the options available for the table, click on “Edit Query”.

![image](https://user-images.githubusercontent.com/116351573/209017551-5221c58e-d74c-4421-9261-09ca4779134d.png)

2.	A new “Power Query Editor” window will open, click on expand arrow button as highlighted in below image which will show the query.

![image](https://user-images.githubusercontent.com/116351573/209017584-1aec6844-5840-4bdf-8f11-a2e7734f78a3.png)

3.	Change the date value in query for measurement period start date and end date, first value is for start date and second value is for end date parameter respectively, so we have set 11/14/2010 as measurement period start date and 11/17/2022 as measurement period end date:
![image](https://user-images.githubusercontent.com/116351573/209017636-afab257e-9d19-4dab-84e2-c523c3219151.png)

After editing the values, click on right tick as highlighted in image below:
![image](https://user-images.githubusercontent.com/116351573/209017679-fd794acf-222b-4c39-bbaa-6cea2b6d47af.png)

4.	If there is an “Edit permission” button, click on it, if there is “Edit Credentials” button, jump to step 8.

![image](https://user-images.githubusercontent.com/116351573/209017715-5ebb2883-4aeb-4b77-8e39-3e63f6639dc0.png)
5.	On new pop-up window click on “Run” button:

![image](https://user-images.githubusercontent.com/116351573/209017747-ebe84dcf-edea-486b-81ef-1bb46951c4e4.png)
6.	Query will be executed, latest result data will be loaded into the table, this will complete query editing, close the “Power Query Editor” window:

![image](https://user-images.githubusercontent.com/116351573/209017776-f4b0728a-c0d9-4509-b648-49bb53febb82.png)

7.	If you see “Edit Credentials” button as shown below, click on it.

![image](https://user-images.githubusercontent.com/116351573/209017813-bf125d9f-fe46-484a-9919-efafdc38003a.png)

8.	A New popup window will appear, select Microsoft account from left-hand side pane and click sign in, sign in with Microsoft account.

![image](https://user-images.githubusercontent.com/116351573/209017839-96f17746-657d-40f1-8c9a-389e4b9c4fc5.png)

9.	After successfully signing in, click on “Connect”.

![image](https://user-images.githubusercontent.com/116351573/209017868-af215960-e699-4736-b5f3-ebe0bc0313de.png)

## Publish the dashboard in PowerBI Service
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
