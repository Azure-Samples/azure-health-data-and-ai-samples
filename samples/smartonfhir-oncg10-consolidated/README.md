# Azure SMART on FHIR & ONC (g)(10) Consolidated Sample

This sample demonstrates how [Azure Health Data Services](https://www.healthit.gov/test-method/standardized-api-patient-and-population-services#ccg), along with either Microsoft Entra ID or Azure AD B2C, can be used to pass the Inferno test suite for ONC [§170.315(g)(10) Standardized API for patient and population services criterion](https://www.healthit.gov/test-method/standardized-api-patient-and-population-services#ccg), which include:
- [Health Level 7 (HL7®) Version 4.0.1 Fast Healthcare Interoperability Resources Specification (FHIR®)](http://hl7.org/fhir/directory.html)
- [United States Core Data for Interoperability (USCDI)](https://www.healthit.gov/isa/us-core-data-interoperability-uscdi)
- [HL7® FHIR® Bulk Data Access (Flat FHIR®) (V1.0.1:STU 1)](https://hl7.org/fhir/uv/bulkdata/STU1.0.1/)
- [FHIR® US Core Implementation Guide STU V3.1.1](http://hl7.org/fhir/us/core/STU3.1.1/)
- [HL7® SMART Application Launch Framework Implementation Guide Release 1.0.0](https://hl7.org/fhir/smart-app-launch/1.0.0/)
- [OpenID Connect Core 1.0 incorporating errata set 1](https://openid.net/specs/openid-connect-core-1_0.html)

While Azure Health Data Services is the core of this sample, some custom behavior is required to fully meet the §170.315(g)(10) Standardized API for patient and population services criteria requirements, mostly around SMART on FHIR authentication. This sample is therefore *not* using only the FHIR Server but other Azure Services with sample code to pass the Inferno tests. You can use this sample as a starting point and reference for building your applications and solutions.

## Sample Components

The below components are deployed with this sample. 
1. Azure Health Data Services with a FHIR Service
    - FHIR Service acts as the backend that stores and retrieves FHIR resources. It supports the integration of SMART on FHIR apps, enabling them to perform a wide range of clinical and analytical operations on health data. This includes querying patient records, updating information, and interoperating with other clinical systems—all within the security and compliance frameworks offered by Azure.
1. Azure API Management
    - APIM is a cloud-based service from Microsoft that helps businesses manage and secure their Application Programming Interfaces (APIs). APIM is used to manage the interactions between client applications and the Azure API for FHIR. It can enforce usage policies, validate tokens, provide caching, log API calls, and handle rate limiting. This ensures that the FHIR server is only accessed via secure and controlled paths.
1. Smart Auth Function App
    - This is an Azure Function which is a serverless compute platform that allows users to develop event-driven applications. The Smart Auth Function App typically handles tasks such as generating and validating tokens, managing sessions, and possibly transforming claims or other security-related operations needed to integrate SMART on FHIR apps securely with Azure Health Data Service (FHIR).
        - Needed for certain SMART operations not supported by the FHIR Service or specific to your EHR.
            - Standalone Launch Handler enables the auth flow for standalone launch scenarios.
            - EHR Launch Handler enables the auth flow for EHR launch scenarios.         
1. Azure Storage Account
    - Needed for Azure Function, assorted static assets, and configuration tables.
1. Auth Context Frontend App
    - This app basically uses Web App Service to deploy UI for user Authorization. The Auth Context Frontend App facilitates the OAuth2 authorization flow. It interacts with Azure Active Directory (AAD) to authenticate users and obtain consent for the required scopes, thereby ensuring that applications receive appropriate access to health data based on user permissions.
     - Needed for the Patient Standalone authorize flow to properly handle scopes. Microsoft Entra ID does not support session based scoping.

The below components will be deployed with this sample but will only be accessible if ONC g(10) compliance is enabled.
1. FHIR Bulk Data Custom Operations
    - This is an Azure Function which is a serverless compute platform that allows users to develop event-driven applications. The FHIR Bulk Data Custom Operations typically handles Bulk Data Operations on FHIR Service.   
        - Currently supports Bulk Export Operations. 

1. Azure Key Vault
    - Azure Key Vault is a cloud-based service that allows you to securely manage keys, secrets and certificates used by cloud applications and services. It will be used to store secrets used for testing Inferno (g)(10) test suite.
        - It stores the secret generated for the App Registration. 
        - It also stores secrets required for the Service Base URL Test Suite.

Working of the sample at high level:

- Routing and SMART Conformance is handled with [Azure API Management API Gateway](https://learn.microsoft.com/azure/api-management/api-management-gateways-overview).
- Authorization as defined by the [SMART on FHIR](https://hl7.org/fhir/smart-app-launch/1.0.0/index.html) and [Bulk Data Access](https://hl7.org/fhir/uv/bulkdata/STU1.0.1/authorization/index.html) Implementation Guide are handled by [Microsoft Entra ID](https://learn.microsoft.com/en-us/entra/fundamentals/whatis) or [Azure AD B2C](https://learn.microsoft.com/en-us/azure/active-directory-b2c/overview) with custom code to enable some specific requirements. 
- Azure AD B2C provides support for third-party identity providers like Google, Apple, etc.
- Bulk Data Export is handled mostly by FHIR Service with some custom code to enable users to access the files they've exported per the Bulk Data Implementation Guide.
- While FHIR Service supports `$export` operations, Azure does not support accessing the files using the same access token used for FHIR Service.
- All FHIR data operations are handled by [FHIR Service in Azure Health Data Services](https://learn.microsoft.com/azure/healthcare-apis/fhir/overview). Azure API for FHIR would also fit here

For more details of how the pieces work together, check out [the technical guide](./docs/technical-guide.md).

<img src="./docs/images/overview-architecture.png" height="700" >


## Sample Deployment

Deployment of this sample requires the creation of supporting Azure services, custom code deployed to Azure Function Apps, and setup in either Microsoft Entra ID or Azure AD B2C. For detailed deployment instructions, check out the [deployment document here](./docs/deployment.md).

This sample is targeted at application developers who are already using Azure Health Data Services or Azure API for FHIR.


## Sample Support

If you are having issues with the sample, please look at the [troubleshooting document](./docs/troubleshooting.md).

If you have questions about this sample, please submit a Github issue. This sample is custom code you must adapt to your own environment and is not supported outside of Github issues. This sample is targeted towards developers with intermediate Azure experience.
