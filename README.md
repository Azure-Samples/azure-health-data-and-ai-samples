# Data Ingest PipeLine

Data Ingestion Workflow will help users to get their data present in HL7 format converted FHIR JSON format and also stored on FHIR server. There are couple of steps involved before the final FHIR JSON is uploaded to FHIR Server :

- HL7 validation

	Here the incoming HL7 message is validated for couple of things like valid message type, valid data types etc. These validations can be extended based on user needs by overriding the classes.
- HL7 Message sequencing

	In certain scenarios, the order of messages is very important to maintain the consistency of the data. To achieve this, all the HL7 messages are sorted based on the value of MSH.9 (in increasing order). So in order to create the files in sequence, first it will check for date/time, then MSH13 (which is message sequence), and finally the HL7 file name.
- HL7 to FHIR conversion 

	Here the HL7 messages are converted to FHIR Bundle JSON using $convert operation. Please click ib [link]("https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/convert-data") to understand more about the "$convert".
- Removing empty resources

	In case there is insufficient information in the incoming HL7 message, empty resources are created by the conversion process. Uploading this to FHIR server might result in erroneous state. So all such empty resources are removed from the converted FHIR bundle JSON.
- Uploading to FHIR server

	FHIR Bundles are uploaded to the FHIR server one by one.


## Architecture Overview

- Please click on the [link](./docs/imgs/service.png) to show the architecture diagram.
- Please click in the [link](./docs/imgs/flowchart.png) to show the flow chart diagram.


## Features

This project framework provides the following features:

* Upload bulk hl7files to fhir server.
* Complete Auditing, Error logging and Retry for throttled transactions.

## Getting Started


### Prerequisites

1. Azure Health Data Services FHIR service.
2. You must have at least a Contributor role in your Azure subscription so that you can create/update the following resources:

    * Resource Group
    * Storage Account
    * App Service Plan
    * Function App
    * Logic App

### Installation
- Run the below command to clone the repo.

    ```
    git clone https://github.com/Azure-Samples/azure-health-data-services-samples.git
    ```

- Set up your local files.

    ```
    azd init 
    ```
-  Run the below command to set up the initial infra for data ingest workflow.

    ```
    azd deploy
    ```
    which will create below resources,
     - Logic App workflow
     - Azure Function
     - Storage Account
     - App Insight
     - Azure FHIR Service
     - Log Analytics Workspace

 -  Run the PowerShell script that makes the connection between the Azure Function App and the Logic App Workflow.
     
     ```
       ./workflow.ps1 --ResourceGroup"<your resourcegroup name>" -- StorageAccount"<your storageaccount>"
     ```       

### Run the Workflow

-  Once the installation is complete, you can upload the hl7files to hl7inputfiles container.
-  Once the files have been successfully uploaded, you can invoke the workflow endpoint via Postman by passing the json below in the body.
    ```
        {
            "containerName" : "hl7inputfiles",
            "proceedOnError" : "true/false",
            "fhirBundleType":"batch/transaction"
        }
    ```
      where,      
  - containername : hl7inputfile container name. This is the location on which the HL7 files are stored
  - proceedOnError : Set this to true if you want to proceed the execution of the remaining workflow even if you get a failed response from any of the step of the workflow 
					 OR
					 Set it to false if you want to exit from the workflow.
  - fhirBundleType : Set "batch" if you want the FHIR bundle type to be "Batch"
					 OR
					 Set it to "transaction" if you want the FHIR bundle type to be "Transaction" .


### Process Flow

Following steps describe the overall flow logic. If any of the steps are not needed OR if any new step needs to be added then user can update the logic app workflow accordingly.

1. When the data ingest pipeline receives an http request, it invokes the hl7 validation function, which reads all hl7 files from the hl7inputfiles container.

2. The NHAPI tool is used to validate the hl7files by the Hl7Validation function. If files are validated successfully, they will move to hl7-validation-succeeded container; otherwise, they will move to hl7-validation-failed container.If any file fails in validation and proceedonError is false, then the process will be stopped.

3. The HL7Sequence function will read the files from the hl7-validation-successful container. It will use NHapi tool to parse the hl7 file data.

4. HL7 file sorting will be done based on DateTimeoffset,SequenceNo and hl7 file name. If the sequence no value is 0 or -1 then will move that file to hl7-sequence-resync container.

5. Based on the HL7Sequence response, the logic app will call the HL7Converter function to send the hl7files to $convert-data. This process will be done in a parallel mode.

6. If $convert-data sends 408, 500, 502, 503, 504, or 429, a retry call will be made with a maximum of three attempts.

7. If conversion fails, the IsConversionFail value will be set to true, and email will be sent for all the failed files.

8. Successfully converted HL7 files will be moved to hl7-converter-succeeded, and failed files will be moved to hl7-converter-failed.

9. All successfully converterd hl7files will create a new json file with FHIR JSON and will store this json to hl7-converter-json container. 

10. The FHIRPostProcess function will be called which will read the HL7 converted files and performs following operations:
	- Remove any empty resources from FHIR JSON 
	- Set the bundle type to batch or transaction as specified in the request input 
	- Store the modified FHIR JSON in hl7-converter-json.

11. If processedonerror is false and the conversionfail value is true, then processing will be stopped and all files will be moved to the hl7-skipped container.  

12. If the above condition is false, all files in the list will be sent to the UploadFHIRJson function, which will send them to the FHIR server in batch order.

13. If the FHIR server sends 408, 500, 502, 503, 504, or 429, a retry call will be made with a maximum of three attempts.

14. If files are successfully sent to the fhir server, then it will be moved to hl7-fhirupload-succeeded, or it will move to hl7-fhirupload-failed.
