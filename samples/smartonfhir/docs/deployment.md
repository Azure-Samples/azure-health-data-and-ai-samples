# Sample Deployment: SMART on FHIR

This document guides you through the steps needed to deploy this sample. This sample deploys Azure components, custom code, and Microsoft Entra ID configuration.

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
  - Elevated access in Microsoft Entra ID and Microsoft Graph to create Application Registrations, assign Microsoft Entra ID roles, and add custom data to user accounts.

- **Test Accounts:**
  - For Microsoft Entra ID:
    - Microsoft Entra ID test account to represent Patient persona. Make sure you have the object id of the user from Microsoft Entra ID.
    - Microsoft Entra ID test account to represent Provider persona. Make sure you have the object id of the user from Microsoft Entra ID.
  - For Azure B2C:
    - Azure B2C test account to represent Patient persona. Make sure you have the object id of the user from Azure B2C.
    - Azure B2C test account to represent Provider persona. Make sure you have the object id of the user from Azure B2C.

- **Azure B2C SetUp:**
  - This setup is exclusively necessary for Smart on FHIR implementation with B2C. If you opt for Microsoft Entra ID, you can bypass this configuration.
  - Follow below mentioned steps:
    - [Create an Azure AD B2C tenant for the FHIR service](https://review.learn.microsoft.com/en-us/azure/healthcare-apis/fhir/azure-ad-b2c-setup?branch=main&branchFallbackFrom=pr-en-us-261649&tabs=powershell#create-an-azure-ad-b2c-tenant-for-the-fhir-service)
    - [Deploy an Azure AD B2C tenant by using an ARM template](https://review.learn.microsoft.com/en-us/azure/healthcare-apis/fhir/azure-ad-b2c-setup?branch=main&branchFallbackFrom=pr-en-us-261649&tabs=powershell#deploy-an-azure-ad-b2c-tenant-by-using-an-arm-template)
    - [Add a test B2C user to the Azure AD B2C tenant](https://review.learn.microsoft.com/en-us/azure/healthcare-apis/fhir/azure-ad-b2c-setup?branch=main&branchFallbackFrom=pr-en-us-261649&tabs=powershell#add-a-test-b2c-user-to-the-azure-ad-b2c-tenant)
    - [Link a B2C user with the fhirUser custom user attribute](https://review.learn.microsoft.com/en-us/azure/healthcare-apis/fhir/azure-ad-b2c-setup?branch=main&branchFallbackFrom=pr-en-us-261649&tabs=powershell#link-a-b2c-user-with-the-fhiruser-custom-user-attribute)
    - [Create custom user flow using custom policy](../docs/create-custom-policy.md)

## 2. Prepare and deploy environment

Next you will need to clone this repository and prepare your environment for deployment by creating two required Azure App Registrations and configuring your environment to use them.

1. Use the terminal or your git client to clone this repo. Open a terminal to the `samples/smartonfhir` folder.
1. Login with the Azure CLI.
   - If you opt for B2C use `az login --tenant <B2CTenantDomainName> --allow-no-subscriptions`.
   - If you opt for Microsoft Entra ID use 
        ```
        az login --tenant <tenant-id>
        azd auth login --tenant-id <tenant-id>
        ```
1. Run `azd env new` to create a new deployment environment, keeping below points in mind.
    - Environment name must not exceed 18 characters in length.
    - Deployment fails if Environment name contains UpperCase Letters.
    - Use numbers and lower-case letters only for Environment name.
    - Environment name will be the prefix for all of your resources.
1. [Create the FHIR Resource App Registration. Use the instructions here](./ad-apps/fhir-resource-app-registration.md). Record the application id and application url for later.
1. [Create the Auth Context Frontend App Registration. Use the instructions here](./ad-apps/auth-context-frontend-app-registration.md). Record the application id and application url for later.
1. Set your deployment environment configuration.
    ```
    azd env set ApiPublisherName "Your Name"
    azd env set ApiPublisherEmail "Your Email"
    ```
1. If you have opted for B2C, then [Create Inferno Standalone Patient App. Use the instructions here](./ad-apps/inferno-test-app-registration.md).
1. If you have opted for B2C, then set the deployment environment configuration.
    ```
    azd env set B2CTenantId <Tenant_ID_Of_B2C>
    azd env set AuthorityURL "https://<YOUR_B2C_TENANT_NAME>.b2clogin.com/<YOUR_B2C_TENANT_NAME>.onmicrosoft.com/<YOUR_CUSTOM_USER_FLOW_NAME>/v2.0"
    azd env set StandaloneAppClientId <STANDALONE_APP_ID_CREATED_IN_STEP_7>
    azd env set SmartonFhirwithB2C true
    ```
    If you have opted for Microsoft Entra ID, then set the deployment environment configuration.
    ```
    azd env set AuthorityURL "https://login.microsoftonline.com/<Microsoft Entra ID Tenant Id>/v2.0" 
    azd env set SmartonFhirwithB2C false
    ```
1. If you have opted for B2C, then Login with the Azure Developer CLI.
    ```
    az login --tenant <tenant-id>
    azd auth login --tenant-id <tenant-id>
    ```
1. Start the deployment of your environment by running the 'azd' command. This action will provision the infrastructure as well as deploy the code, which is expected to take about an hour.
    ```
    azd up
    ```
    - When running this command, you must select the `subscription name` and `location` from the drop-down menus to specify the deployment location for all resources. 
    - Please be aware that this sample can only be deployed in the EastUS2, WestUS2, or CentralUS regions. Make sure you choose one of these regions during the deployment process.
    - The azd provision command will prompt you to enter values for the `B2CTenantId`, `StandaloneAppClientId`, `existingResourceGroupName` and `fhirid` parameters:
        - `B2CTenantId` : This parameter takes Tenant ID of your B2C Tenant which you deployed earlier.
            - Note: If you have opted for Microsoft Entra ID you can skip this parameter.
        - `StandaloneAppClientId` : This parameter takes Application ID / Client ID of Inferno Standalone Patient App which you created earlier.
            - Note: If you have opted for Microsoft Entra ID you can skip this parameter. 
        - `existingResourceGroupName` : This parameter allows you to decide whether to deploy this sample in an existing resource group or to create a new resource group and deploy the sample. Leaving this parameter empty will create a new resource group named '{env_name}-rg' and deploy the sample. If you provide an existing resource group, the sample will be deployed in that resource group.
          - Note: If you are using an existing resource group, make sure that it does not already have a SMART on FHIR resource already deployed, because multiple samples in the same resource group are not supported.
          - Note: SMART on FHIR will need to be deployed in the same resource group as the associated FHIR server. 
        - `fhirid`: This parameter allows you to decide whether to use an existing FHIR service or create a new one. Leaving this parameter empty will create a new FHIR service. If you wish to use an existing FHIR server, input the FHIR instance ID. Below are steps to retrieve the FHIR instance ID: 
            1. Navigate to your FHIR service in Azure Portal.
            2. Click on properties in the left menu.
            3. Copy the "Id" field under the "Essentials" group.    
        - Some important considerations when using an existing FHIR service instance:
            - The FHIR server instance and SMART on FHIR resources are expected to be deployed in the same resource group, so enter the same resource group name in the `existingResourceGroupName` parameter.
1. Add fhirUser claim to your test users. You will need to add the `fhirUser` claim to each of your test user accounts. For the patient test user, the `fhirUser` needs to be `<Complete Fhir Url>/Patient/PatientA` to collaborate with the sample data. For the practitioner test user, the `fhirUser` needs to be `<Complete Fhir Url>/Practitioner/PractitionerC1`.

   Changing an Microsoft Graph directory extensions is done through API requests to Microsoft Graph. You can use the command below to set the `fhirUser` claim via a helper script for your patient test user. You will just need the `object id` of your patient test user. In a production scenario, you would integrate this into your user registration process.

    1. Create a Microsoft Graph Directory Extension to hold the `fhirUser` information for users.
    
        Windows:
        ```powershell
        powershell ./scripts/Add-FhirUserInfoToUser.ps1 -ApplicationId "<If you opted for B2C pass B2C_EXTENSION_APP_ID otherwise for Microsoft Entra ID pass Fhir Resource Application Id>" -UserObjectId "<Patient Object Id>" -FhirUserValue "<Complete Fhir Url>/Patient/PatientA"
        ```

        Mac/Linux:
        ```bash
        pwsh ./scripts/Add-FhirUserInfoToUser.ps1 -ApplicationId "<If you opted for B2C pass B2C_EXTENSION_APP_ID otherwise for Microsoft Entra ID pass Fhir Resource Application Id>" -UserObjectId "<Patient Object Id>" -FhirUserValue "<Complete Fhir Url>/Patient/PatientA"
        ```
    1. If you have opted for Microsoft Entra ID, then make sure your test user has the role `FHIR SMART User` assigned to your FHIR Service deployed as part of this sample.
        - This role is what enables the SMART scope logic with your access token scopes in the FHIR Service.
> [!IMPORTANT]  
> If you are using an existing FHIR server, please note that while deployment the FHIR server Audience URL has changed to the new Application Registration ID URL. If you have downstream apps that were using the previous FHIR server Audience URL, you will need to update those to point to the new URL.  


*NOTE:* This will take around 15 minutes to deploy. You can continue the setup below. 

## 3. Complete Setup of FHIR Resource and Auth Context Frontend Applications

### For Microsoft Entra ID user only - Assign Role to the Deployed or Existing Fhir Service

Ensure your test user has the role `FHIR SMART User` assigned to your Existing or Deployed Fhir Service as part of this sample.
    - This role is what enables the SMART scope logic with your access token scopes in the FHIR Service.

### Assign Permissions for the Auth Custom Operation API

As part of the scope selection flow, the Auth Custom Operation Azure Function will modify user permissions for the signed in user. 

If you have opted for Microsoft Entra ID - This requires granting the Azure Managed Identity behind Azure Functions Application Administrator (or similar access).
1. Open the Azure Function for the SMART Auth Custom Operations. It will be suffixed by `aad-func`. Copy the Managed Identity for the next steps.
1. Open Microsoft Entra ID and navigate to `Roles and Administrators`. Open the `Application Administrator` role.
1. Add the Azure Function Managed Identity to this Microsoft Entra ID role.
    <br /><details><summary>Click to expand and see screenshots.</summary>
    ![](./images/deployment/4_copy_function_managed_identity.png)
    ![](./images/deployment/4_open_application_administrator.png)
    ![](./images/deployment/4_assign_function_application_administrator.png)
    </details>
    <br />

If you have opted for B2C - This is required to access the applications registered in the B2C tenant from Azure Function to perform the SMART Auth Custom Operations. You need to provide client secret of Standalone application in key vault. 
1. In the resource group that matches your environment, open the KeyVault with the suffix -kv.
1. Add a new secret that corresponds to the Standalone Application you just generated.
    - Name: `standalone-app-secret`
    - Secret: The secret you generated for the Standalone application

### Set the Auth User Input Redirect URL

1. Open the resource group created by step 3. Find the Azure API Management instance.
1. Copy the Gateway URL for the API Management instance.
1. Open your Application Registration for the Auth Context Frontend you created before deployment. Add `<gatewayURL>/auth/context/` as a sinple-page application redirect URI. Make sure to add the last slash.
    - For example: `https://myenv-apim.azure-api.net/auth/context/`
<br /><details><summary>Click to expand and see screenshots.</summary>
![](./images/deployment/4_save_redirect_uri.png)
</details>

*NOTE:* Changes made to Application Registration in Azure B2C Tenant takes time to reflect.