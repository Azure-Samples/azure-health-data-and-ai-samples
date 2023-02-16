# FHIR server(Destination) and FHIR Bulk Loader App configuration.

This will focus on how to configure destination FHIR server and FHIR Bulk Loader Application settings.

# Destination FHIR server.

1. Enable Autoscaling
    - Please follow [link](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/autoscale-azure-api-fhir) to enable autoscaling and to estimate throughput RU's required.

    **NOTE** : Need to open Azure support ticket to enable autoscaling for RU's.
2. Number of RUs
    - Please follow [link](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/configure-database) to configure the database RU's as per the data size. 

     **NOTE** : If requirement for RU's is more than 100k, please open Azure support ticket.

3. Number of Nodes.
    - Default number of nodes per FHIR server is 2. 
    - This will auto scale as per the load on FHIR server.
    
    **NOTE** : User can increase the default number of node per FHIR server by opening Azure support ticket.

4. Throttling Concurrency Limit.
    - Default limit is 15.
    - If user is getting more error for 429's(Throttled) while importing data to fhir server they can increase the same by opening the Azure support ticket.

**NOTE** : All requirements can be put into single support ticket. No need to open seperate ticket for each configuration settings.

# FHIR Bulk Loader Application.
1. App Service Plan
    - By default B2 service plan is deployed for the FHIR Bulk Loader.
    - User can [scale up](https://learn.microsoft.com/en-us/azure/app-service/manage-scale-up) App Plan as per their requirement.
2. Number of instance
    - By default 2 instances are deployed for FHIR Bulk Loader.
    - User can [scale out](https://learn.microsoft.com/en-us/azure/azure-monitor/autoscale/autoscale-get-started) the instance count manually or auto scaling. 
3. Number of FHIR resources per bundle file.
    - By default 200 FHIR resources are creating per bundle file during NDJSON file processing.
    - This number can be adjusted as per the user requirement.
    - Following are the steps to configure this setting.
        1. Open the configuration section in FHIR Bulk Loader azure function.
        2. Click on New application setting.

        ![App Config](images/App_config.png)

        3. Add the below name and value as per the requirement in the setting and click ok.

        ![App setting](images/App_setting.png)

        4.Once above steps are done click save.

        ![save](images/save.png)

    **NOTE**: When we change this configuration number of bundle files will be created according and user can manage the size of each bundle using this setting.
