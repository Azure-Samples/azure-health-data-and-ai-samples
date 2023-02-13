# Data Ingest PipeLine

Data Ingest Workflow will help users to upload the hl7 files to Azure Fhir server.This will validate,convert the HL7files to fhir json and send data to fhir service.

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

-  Run the below command to set up the initial infra for data ingest workflow.

    ```
       az deployment sub create --name demoSubDeployment --location eastus --template-file main.bicep --parameters main.parameters.json
    ```
    which creates below resources,
     - Logic App workflow
     - Azure Function
     - Storage Account
     - App Insight
     - Azure FHIR Service
     - Log Analytics Workspace


 -  Run the Powershell script which makes the connection bewtween Azure function app and Logic app workflow.
     
     ```
       ./workflow.ps1 --ResourceGroup"<your resourcegroup name>" -- StorageAccount"<your storageaccount>"
     ```       

### Run the Workflow

-  Once installation done successfully, you can upload the hl7files into hl7inputfiles.
-  Once files uploaded sucessfully, you can trigger the workflow endpoint through postman by passing below json in body.
    ```
        {
            "containerName" : "hl7inputfiles",
            "proceedOnError" : "true/false",
            "fhirBundleType":"batch/transcation"
        }
    ```
      where,      
  - containername : hl7inputfile container name
  - proceedOnError : Set true if you don't want to stop the work flow even if getting failed response from FHIR server or set to false if need to stop the workflow.
  - fhirBundleType : Set batch if need to send data in batch mode or else set transcation to send one by one. 






