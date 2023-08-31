# Azure Health Data Services Samples Repo

The Azure Health Data Services Samples Repo is a set of sample apps and sample code provided to help you get started with Azure Health Data Services, learn how to use our products, and accelerate your implementations.

This project hosts open-source **samples** for Azure Health Data Services. To learn more about Azure Health Data Services, please refer to the managed service documentation [here.](https://learn.microsoft.com/azure/healthcare-apis/healthcare-apis-overview)

## Samples

This project provides samples outlining example implementations of various use cases across stages of health data workflows. The "samples" folder contains all the sample apps organized by use case. The samples are listed here:

<!---
### Data ingestion into Health Data Services

|Sample|Description|
| --- | --- |
| [Sample HL7v2 Data Ingestion Pipeline]() | Sample app that shows how to ingest HL7v2 data into FHIR server, including conversion and validation. |

### Analytics and machine learning

|Sample|Description|
| --- | --- |
| FHIR Delta Lake with Databricks | End-to-end sample showing data from FHIR Service into Databricks Delta Lake Bronze, Silver, and Gold Levels |
| PowerBI Dashboard using Analytics pipelines | Sample showing how to query FHIR data in Parquet file format (in Azure Data LAke) and Serverless SQL tables to calculate digital quality measures and visualize stratified measure data in PowerBI.|

### Other integrations
|Sample|Description|
| --- | --- |
| FHIR to HL7v2 format for ingestion back into an EHR (coming soon)| Sample to convert FHIR data to HL7v2 format suitable for ingestion into an EHR. |

--->

### Data ingestion

|Sample|Description|
| --- | --- |
| [Migrate data from one Azure API for FHIR server to another API for FHIR server](samples/fhir-to-fhir/api-for-fhir-to-api-for-fhir) | Sample app for copying/migrating data from one Azure API for FHIR server to another Azure API for FHIR server. |

### Sample transactions

|Sample|Description|
| --- | --- |
| [Sample Postman queries](samples/sample-postman-queries) | Learn how to interact with FHIR data using Postman with this starter Postman collection of common Postman queries used to query FHIR server, including FHIR searches, creating, reading, updating, and deleting requests for FHIR resources, and other operations.|

### Analytics and machine learning

|Sample|Description|
| --- | --- |
| [Visualize Digital Quality Measures in PowerBI leveraging FHIR parquet data in Data Lake](samples/analytics-visualization) | Sample demonstrates how to calculate example quality measures from FHIR data by querying flattened FHIR parquet file data in Synapse Analytics and visualizing the results in Power BI.|
| [Integrate Azure Health Data Services FHIR data with Delta Lake on Azure Databricks](samples/azuredatabricks-deltalake/) | Learn how to use Azure Databricks with Azure Health Data Services. Sample demonstrates how to automatically connect data from the FHIR Service into analytics platforms on Azure Databricks Delta Lake using the Analytics Connector. |

### SMART on FHIR

|Sample|Description|
| --- | --- |
| [SMART on FHIR sample](samples/smartonfhir) | Sample demonstrating using [SMART on FHIR](http://hl7.org/fhir/smart-app-launch/index.html) to interact with FHIR data in [Azure Health Data Services](https://learn.microsoft.com/azure/healthcare-apis/fhir/smart-on-fhir).|

### Patient and population services (g)(10) (including SMART on FHIR) sample

|Sample|Description|
| --- | --- |
| [Patient and Population Services (g)(10) (including SMART on FHIR) sample](samples/patientandpopulationservices-smartonfhir-oncg10) | Sample utilizing [Azure Health Data Services](https://learn.microsoft.com/azure/healthcare-apis/fhir/smart-on-fhir) to demonstrate to health organizations with the steps to meet the [§170.315(g)(10) Standardized API for patient and population services criterion](https://www.healthit.gov/test-method/standardized-api-patient-and-population-services#ccg).|

### DICOM service

|Sample|Description|
| --- | --- |
| [DICOM service demo environment](/samples/dicom-demo-env/) | This sample provisions a full end-to-end demo environment of a simplified on-prem radiology network in an Azure resource group. Instructions are provided for configuring and using the DICOM router and ZFP viewer included in the environment. |

### FHIR Service and Terminology Service Integration

|Sample|Description|
| --- | --- |
| [FHIR Service and Terminology Service Integraion](/samples/fhir-terminology-service-integration/) | Sample shows how an external terminology service can be used in conjunction with the AHDS FHIR service by providing a unified endpoint for AHDS FHIR service as well as Terminology Operations. |

### MedTech service

|Sample|Description|
|------|-----------|
|[MedTech service mappings](/samples/medtech-service-mappings/)|The [MedTech service](https://learn.microsoft.com/azure/healthcare-apis/iot/overview) scenario-based samples provide conforming and valid [device](https://learn.microsoft.com/azure/healthcare-apis/iot/overview-of-device-mapping) and [FHIR destination](https://learn.microsoft.com/azure/healthcare-apis/iot/overview-of-fhir-destination-mapping) mappings and test device messages to assist with authoring and troubleshooting mappings.|

**NOTE:** These code samples are simplified scenarios showing how you can use Azure Health Data Services. These samples should be used for testing purposes only with sample data.

## Resources

[Azure Health Data Services documentation](https://learn.microsoft.com/azure/healthcare-apis/healthcare-apis-overview)

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit [Contributor License Agreements](https://cla.opensource.microsoft.com).

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Disclaimers

The Azure Health Data Services Samples Repo is an open-source project. It is not a managed service, and it is not part of Microsoft Azure Health Data Services. The sample apps and sample code provided in this repo are used as examples only. You bear sole responsibility for compliance with local law and for any data you use when using these samples. Please review the information and licensing terms on this GitHub website before using the Azure Health Data Services Samples repo.

The Azure Health Data Services Samples Github repo is intended only for use in transferring and formatting data. It is not intended for use as a medical device or to perform any analysis or any medical function and the performance of the software for such purposes has not been established. You bear sole responsibility for any use of this software, including incorporation into any product intended for a medical purpose.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.

FHIR® is a registered trademark of Health Level Seven International, registered in the U.S. Trademark Office and is used with their permission.
