# Error Handling.

This will focus on how to handle the error during the data movement process.

# Export data
1. Export error.
    - User can go through the [troubleshoot section](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/export-data) to handle issue on exporting the data.
2. Check export status.
    - User can fetch the URL for the export data and check the status.
    - Below are the steps:
        1. Once user hit the export command, they will get operation export URL in response header section under Content-Location

        Example:
        ```Powershell
        https://<<FHIR Server URL>>/_operations/export/<Operation Number>
        ```
        2. User hit the URL and can get the operation details like output, error and issues occurred during export operation.

# Copying data
1. Script execution failure.
    - Common error:

        - User not logged in to correct tenant.
        - User not passing correct HTTP URL of source and destination container while executing the script
        - User not having correct storage permission to perform copy operation.

    Please check the storage access and correct argument passing while executing the script.


# FHIR Bulk Loader
Once the data is copied to 'ndjson' destination container, FHIR Bulk loader starts the process.

- User can get the details for the error
    1. NDJSON error:
        - When NDJSON file failed to process. The FHIR Bulk loader will move the file to 'ndjsonerr' container with the details.
        - User can access these files and make required changes and re-pushed the files to 'ndjson' container manually.
    2. Bundle error:
        - When Bundle file failed to process. The FHIR Bulk loader will move the file to 'bundleserr' container with the details.
        - User can access these files and make required changes and re-pushed the files to 'bundle' container manually.
        
        **NOTE** : There are cases when we get error from FHIR server like internal server error(500) or throttled(429). For such case user can manually re-pushed the files to 'bundle' container manually without making changes in file(but need to change file type from error to json before pushing)
