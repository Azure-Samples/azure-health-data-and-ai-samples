# Error Handling

This will focus on how to handle errors that may arise during the data movement process.

# Export data
1. Export error.
    -  Please see the [troubleshooting section](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/export-data) to handle issues on exporting the data.
2. Check export status.
    - Fetch the URL for the export data and check the status.
    - Below are the steps:
        1. Once you hit the export command, you will get the operation export URL in the response header section under Content-Location

        Example:
        ```Powershell
        https://<<FHIR Server URL>>/_operations/export/<Operation Number>
        ```
        2. Visit the URL and to get the operation details like output, error and issues occurred during export operation.

# Copying data
1. Script execution failure.
    - Common errors:

        - You are not logged in to correct tenant.
        - You are not passing correct HTTP URL of source and destination container while executing the script
        - You are not having correct storage permission to perform copy operation.

    Please check the storage access and correct argument passing while executing the script.


# FHIR Bulk Loader
Once the data is copied to 'ndjson' destination container, FHIR Bulk loader starts the process.

- You can get the details for the error
    1. NDJSON error:
        - When a NDJSON file fails to process, the FHIR Bulk loader will move the file to 'ndjsonerr' container with the details.
        - You can access these files and make required changes, then re-push the files to 'ndjson' container manually.
    2. Bundle error:
        - When a Bundle file fails to process, the FHIR Bulk loader will move the file to 'bundleserr' container with the details.
        - You can access these files and make required changes, then re-push the files to 'bundle' container manually.
        
        **NOTE** : 
        1. There are cases when you may receive an error from the FHIR server like internal server error (500) or throttled (429). For such case you can re-push the files to 'bundle' container manually without making changes in file.
        2. When you need to re-push the files, please change file type from error back to json.

