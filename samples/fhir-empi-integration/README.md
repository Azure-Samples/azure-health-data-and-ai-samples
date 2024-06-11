[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fazure-quickstart-templates%2Fmaster%2Fquickstarts%2Fmicrosoft.storage%2Fstorage-account-create%2Fazuredeploy.json)

# External Enterprise Master Patient Index (EMPI) Service integration with AHDS FHIR service


Sample shows how an external EMPI service can be used in conjunction with the AHDS FHIR service by providing a unified endpoint for AHDS FHIR service as well as EMPI Operations.

## Architecture
This architecture explains how a web application communicates with a EMPI service and FHIR service via an Azure API management service (APIM) and EMPI Connector (Azure Function App).


![](./images/Architecture.png)


## The Architecture components
- **Static Web App**: Blazor UI Application for $match and CRUD operations for patient on FHIR service ad EMPI service
- **API Management service (APIM)**: The call from the UI (Static Web Apps) hits the APIM and diverts the call as per the request to EMPI Connector, EMPI connector inteatcts with FHIR service and EMPI service 
- **EMPI Connector App**: 
- **EMPI service**: It contains demographic data for patients
- **AHDS FHIR service**: AHDS FHIR Service, contains healthcare data including patients


## Prerequisites

 * An Azure account with an active subscription.
	- You need access to create resource groups, resources, and role assignments in Azure

 * AHDS FHIR service deployed in Azure. For information about how to deploy the FHIR service, see [Deploy a FHIR service](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/fhir-portal-quickstart).

 * Postman installed locally. For more information about Postman, see [Get Started with Postman](https://www.getpostman.com/).
 
 * Knowledge of how to access the FHIR service using Postman, including [registering the client application to access the FHIR service](https://github.com/microsoft/azure-health-data-services-workshop/blob/main/resources/docs/Postman_FHIR_service_README.md#step-1---create-an-app-registration-for-postman-in-aad) and granting [FHIR Data Contributor](https://github.com/microsoft/azure-health-data-services-workshop/blob/main/resources/docs/Postman_FHIR_service_README.md#step-2---assign-fhir-data-contributor-role-in-azure-for-postman-service-client) permissions. In case you don't have postman setup to access FHIR Service, Please follow this tutorial: [Access using Postman | Microsoft Learn](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/use-postman).

 * Working External EMPI Service URL and authentication details.

 * [.NET 6.0](https://dotnet.microsoft.com/download)

 * [Azure Command-Line Interface (CLI)](https://docs.microsoft.com/cli/azure/install-azure-cli)

 * [Azure Developer CLI](https://docs.microsoft.com/azure/developer/azure-developer-cli/get-started?tabs=bare-metal%2Cwindows&pivots=programming-language-csharp#prerequisites)

 * Clone this repo.

 ### Prerequisite check

- In a terminal or command window, run `dotnet --version` to check that the .NET SDK is version 6.0 or later.
- Run `az --version` and `azd version` to check that you have the appropriate Azure command-line tools installed.
- Login to the Azure CLI
- Launch Postman app.

### Azure API Management Service

The APIM handles the routing and authentication part for external EMPI service and AHDS FHIR service.

The APIM routes $match, POST, PUT and DELETE operations to a third party EMPI service and AHDS FHIR Service.

- **Useful links for APIM:**
	1. [About Azure APIM](https://learn.microsoft.com/en-us/azure/api-management/api-management-key-concepts)
	2. [Useful APIM terminologies](https://learn.microsoft.com/en-us/azure/api-management/api-management-terminology)
	3. [Azure APIM backends](https://learn.microsoft.com/en-us/azure/api-management/backends?tabs=bicep)
    4. Create an Azure APIM instance following steps [here](https://learn.microsoft.com/en-us/azure/api-management/get-started-create-service-instance)
	5. How to add [API](https://learn.microsoft.com/en-us/azure/api-management/add-api-manually) in Azure APIM instance.
	6. Azure APIM [Policies](https://learn.microsoft.com/en-us/azure/api-management/policies/)
	7. How to configure [authentication and authorization](https://learn.microsoft.com/en-us/azure/api-management/authentication-authorization-overview) in APIM.


### Static Web App (UI) and Postman queries

UI application and Postman queries use common APIM endpoint for EMPI service operations and FHIR service Operations.

The UI application demonstartes $match and CRUD operations for patient, those operations are routed to external EMPI service by EMPI Connector app. 

The UI Application also demonstartes operations for searching Observation resources from FHIR service and saving translated Observation resources to FHIR service, the search and save operations are routed to AHDS FHIR Service by APIM.

### Postman Queries

The postman queries to demonstare Common Endpoint Application (APIM) routing calls to external EMPI service and AHDS FHIR Service via single endpoint are available under `FHIR-EMPI  Integration` folder in `Fhir-EMPI Collection` postman collection available in this repo. For Queries in this collection, we are using APIM URL as our base URL and auth token of FHIR service to authenticate requests.

## Setting up application locally 
### Visual Studio

* Clone the repo, under path *\samples\fhir-empi-integration\webapp, Open the `FhirBlaze.sln` project in Visual Studio.
* This application is based on sample app [here](https://github.com/microsoft/azure-health-data-services-workshop/tree/main/Challenge-10%20-%20Optional%20-%20FhirBlaze%20(Blazor%20app%20dev%20%2B%20FHIR)), please refer Readme file for configuration of project. Follow step 1 & 3 only, skip step 2.
* Set FhirBlaze project as StartUpProject
* Run FhirBlaze Application.

## UI Application Walkthrough:

1. After launching UI application user will be redirected to below landing page, Click on login, select/enter username and password.

    <img src="./images/UI1.png" height="380">

2. On successful login, user can see the username in top right corner as highlighted.

	<img src="./images/UI2.png" height="380">