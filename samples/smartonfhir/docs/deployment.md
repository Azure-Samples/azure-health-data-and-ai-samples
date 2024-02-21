# Sample Deployment: SMART on FHIR

This document guides you through the steps needed to deploy this sample. This sample deploys Azure components, custom code, and Azure Active Directory configuration.

*Note:* This sample is not automated and on average will require at least a couple of hours to deploy end to end.

## 1. Prerequisites

In order to deploy this sample, you will need to install some Azure tools, ensure the proper administrator access to an Azure subscription / tenant, and have test user accounts for impersonating the patient and practitioner personas.

Make sure you have the pre-requisites listed below
- **Installation:**
  - [Git](https://git-scm.com/) to access the files in this repository.
  - [Azure CLI Version 2.51.0 or Greater](https://learn.microsoft.com/cli/azure/install-azure-cli) to run scripts that interact with Azure.
  - [Azure Developer CLI Version 1.2.0 or Greater](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd?tabs=baremetal%2Cwindows) to deploy the infrastructure and code for this sample.
  - [Visual Studio](https://visualstudio.microsoft.com/), [Visual Studio Code](https://code.visualstudio.com/), or another development environment (for changing configuration debugging the sample code).
  - [Node Version 18.17.1/ NPM Version 10.2.0](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm) for building the frontend application and installing the US Core FHIR Profile.
  - [.NET SDK Version 7.0.400](https://learn.microsoft.com/dotnet/core/sdk) installed (for building the sample).
  - [PowerShell Version 5.1.22621.2428 or Greater](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) installed for running scripts (works for Mac and Linux too!).

- **Access:**
  - Access to an Azure Subscription where you can create resources and add role assignments.
  - Elevated access in Azure Active Directory (AD) and Microsoft Graph to create Application Registrations, assign Azure Active Directory roles, and add custom data to user accounts.

- **Test Accounts:**
  - Azure Active Directory test account to represent Patient persona. Make sure you have the object id of the user from Azure Active Directory.
  - Azure Active Directory test account to represent Provider persona. Make sure you have the object id of the user from Azure Active Directory.

- **Azure B2C SetUp:**
  - Follow below mentioned steps from [this document](https://review.learn.microsoft.com/en-us/azure/healthcare-apis/fhir/azure-ad-b2c-setup?branch=main&branchFallbackFrom=pr-en-us-261649&tabs=powershell) to setup Azure B2C.
    - Create an Azure AD B2C tenant for the FHIR service
    - Deploy an Azure AD B2C tenant by using an ARM template
    - Add a test B2C user to the Azure AD B2C tenant
    - Link a B2C user with the fhirUser custom user attribute
    - Create a new B2C user flow

## 2. Prepare and deploy environment

Next you will need to clone this repository and prepare your environment for deployment by creating two required Azure App Registrations and configuring your environment to use them.

1. Use the terminal or your git client to clone this repo. Open a terminal to the `samples/smartonfhir` folder.
1. Login with the Azure CLI using `az login --tenant <B2CTenantName> --allow-no-subscriptions`.
1. Run `azd env new` to create a new deployment environment.
    - *NOTE:* Environment name will be the prefix for all of your resources.
1. [Create the FHIR Resource App Registration in B2C Tenant. Use the instructions here](./ad-apps/fhir-resource-app-registration.md). Record the application id and application url for later.
1. [Create the Auth Context Frontend App Registration in B2C Tenant. Use the instructions here](./ad-apps/auth-context-frontend-app-registration.md). Record the application id and application url for later.
1. Set your deployment environment configuration.
    ```
    azd env set ApiPublisherName "Your Name"
    azd env set ApiPublisherEmail "Your Email"
    ```
1. [Create Inferno Standalone Patient App.Use the instructions here](./ad-apps/inferno-test-app-registration.md).
1. Login with the Azure Developer CLI. Specify the tenant where you want to deploy SMART on FHIR sample resource if you have more than one. `azd auth login` or `azd auth login --tenant-id <AD-tenant-id>`. Also login with the Azure CLI using `az login`.
1. Finally, deploy your environment by running azd. This command will provision infrastructure and deploy code.  It will take about an hour.
    - During the execution of this command, you will need to select `subscription name` and `location` from the drop down to specify where all resources will get deployed. 
      - Please note: This sample can only be deployed in EastUS2, WestUS2, or CentralUS regions. Please choose one of those regions when doing the deployment.  
    - To create a new resource group for SMART on FHIR resources deployment, leave the `existingResourceGroupName` parameter blank; otherwise, enter the name of an existing resource group where you want to deploy all of your SMART on FHIR resources. 
    - Provide parameter values as below 
        ```
         - B2CTenantId: <Tenant_ID_Of_B2C>
         - B2cAuthorityURL: https://<YOUR_B2C_TENANT_NAME>.b2clogin.com/<YOUR_B2C_TENANT_NAME>.onmicrosoft.com/<YOUR_USER_FLOW_NAME>
         - FhirResourceAppId: <FHIR_RESOURCE_APP_ID_CREATED_IN_STEP_4>
         - StandaloneAppClientId: <STANDALONE_APP_ID_CREATED_IN_STEP_7>
         - smartonfhirwithb2c:  true 
         ```
    - Multiple SMART on FHIR sample apps can not be deployed in same resource group.
    - You can continue the setup below. 
    ```
    azd up
    ```

*NOTE:* This will take around 15 minutes to deploy.

## 3. Complete Setup of FHIR Resource and Auth Context Frontend Applications

### Assign Azure AD Permissions for the Auth Custom Operation API

As part of the scope selection flow, the Auth Custom Operation Azure Function will modify user permissions for the signed in user. This requires accessing the applications registered in B2C tenant Azure Function for the SMART Auth Custom Operations. You need to provide client secret of Standalone application in key vault. 

1. In the resource group that matches your environment, open the KeyVault with the suffix -kv.
1. Add a new secret that corresponds to the Standalone Application you just generated.
    - Name: `standalone-app-secret`
    - Secret: The secret you generated for the Standalone application

<br />

### Set the Auth User Input Redirect URL

1. Open the resource group created by step 3. Find the Azure API Management instance.
1. Copy the Gateway URL for the API Management instance.
1. Open your Application Registration for the Auth Context Frontend you created before deployment. Add `<gatewayURL>/auth/context/` as a sinple-page application redirect URI. Make sure to add the last slash.
    - For example: `https://myenv-apim.azure-api.net/auth/context/`

<br />
<details>
<summary>Click to expand and see screenshots.</summary>

![](./images/deployment/4_save_redirect_uri.png)
</details>
<br />

