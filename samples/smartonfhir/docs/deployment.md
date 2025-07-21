> [!TIP]
> *If you encounter any issues during configuration, deployment, or testing, please refer to the [Trouble Shooting Document](./troubleshooting.md)*

# Sample Deployment: SMART on FHIR

This document guides you through the steps needed to deploy this sample. This sample deploys Azure components, custom code, and Microsoft External Entra ID configuration.

*Note:* This sample is not automated and on average will require at least a couple of hours to deploy end to end.

## 1. Prerequisites

In order to deploy this sample, you will need to install some Azure tools, ensure the proper administrator access to an Azure subscription / tenant, and have test user accounts for impersonating the patient and practitioner personas.

Make sure you have the pre-requisites listed below
- **Installation:**
  - [Git](https://git-scm.com/) to access the files in this repository.
  - [Azure CLI Version 2.51.0 or Greater](https://learn.microsoft.com/cli/azure/install-azure-cli) to run scripts that interact with Azure.
  - [Azure Developer CLI Version 1.9.0 or Greater](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd?tabs=baremetal%2Cwindows) to deploy the infrastructure and code for this sample.
  - [Visual Studio](https://visualstudio.microsoft.com/), [Visual Studio Code](https://code.visualstudio.com/), or another development environment (for changing configuration debugging the sample code).
  - [Node Version 18.17.1/ NPM Version 10.2.0](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm) for building the frontend application and installing the US Core FHIR Profile.
  - [.NET SDK Version 8+](https://learn.microsoft.com/dotnet/core/sdk) installed (for building the sample).
  - [PowerShell Version 7 or Greater](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) installed for running scripts (works for Mac and Linux too!).

- **Access:**
    The access requirements depend on the Identity Provider you choose:
    -   **For External Entra ID:**
        - Need to have admin access to an External Entra ID Tenant to create application registration, role assignments, create user accounts.
  
## 2. Prepare and deploy environment

Next you will need to clone this repository and prepare your environment for deployment by creating required Azure App Registrations and configuring your environment to use them.

1. Use the terminal or your git client to clone this repo. Open a terminal to the `samples/smartonfhir` folder.
1. Login with the Azure CLI.

   - **For External Entra ID:**
        ```
        az login --tenant <Entra-External-ID-Tenant-Domain-Name> --allow-no-subscriptions
        ```
1. Run `azd env new` to create a new deployment environment, keeping below points in mind.
    - Environment name must not exceed 18 characters in length.
    - Deployment fails if Environment name contains UpperCase Letters.
    - Use numbers and lower-case letters only for Environment name.
    - Environment name will be the prefix for all of your resources.
1. [Create the FHIR Resource App Registration. Use the instructions here](./ad-apps/fhir-resource-app-registration.md). Record the application id and application url for later.
1. [Create the Auth Context Frontend App Registration. Use the instructions here](./ad-apps/auth-context-frontend-app-registration.md). Record the application id and application url for later.
1. Set the deployment environment configuration as below
    ```powershell
    azd env set ApiPublisherName "Your Name"
    azd env set ApiPublisherEmail "Your Email"
    ```
    ```powershell
    azd env set B2CTenantId <Tenant_ID_Of_Entra_External_ID>
    azd env set AuthorityURL "https://<YOUR_EntraExternalID_TENANT_NAME>.ciamlogin.com/<Tenant_ID_Of_Entra_External_ID>/v2.0"
    azd env set SmartonFhirwithB2C true
    ```
1. To begin the sample deployment, you need to be logged into the appropriate tenant.

    - **For External Entra ID**: Although you logged into the External Entra ID tenant in step 1, you still need to log in to the Azure tenant using below commands. 
        ```
        az login --tenant <tenant-id>
        azd auth login --tenant-id <tenant-id>
        ```
1. Initiate the environment deployment by executing the `azd up` command. This will handle both the infrastructure provisioning and code deployment, which should take around one hour to complete.
    
    *Note*- This command requires at least `PowerShell 7`. Running it in any earlier version may result in failure.

    ```
    azd up
    ```
    *Note:- When executing the `azd up` command, you will be asked to provide several values. Below, you will find a detailed explanation of each prompt.*
    
    **Deployment Instructions**

    When running the `azd up` command, you will need to select the `subscription name` and `location` from the drop-down menus to specify where to deploy all resources. Note that this sample can only be deployed in the `EastUS2, WestUS2, or CentralUS` regions. Ensure you choose one of these regions during deployment.

    The `azd up` command will prompt you to enter values for the following parameters:   
    
    - `B2CTenantId` : 
        - Enter the Tenant ID of your External Entra ID Tenant deployed earlier.           
    
    - `enableVNetSupport`: 
     
        - This parameter accepts a boolean (true/false) value.
 
        - When set to false, the following resources are deployed with mentioned configurations. User will not be able to create private endpoints and will not be able to setup private network.
            1. API Management (APIM): Deployed in the Consumption tier.
            2. App Service Plan : Deployed in the Dynamic tier. 
            3. Static Web App: Deployed in the Free tier.
            4. Function Apps and App Service Plan: Utilizes Linux as the operating system.
        
        - When set to true, the following resources are deployed in the Standard/Premium tier to enable private endpoint creation necessary for Virtual Network Support. 
            1. API Management (APIM): Deployed in the Premium tier.
            2. App Service Plan and Static Web App: Deployed in the Standard tier.
            3. Function Apps and App Service Plan: Utilizes Windows as the operating system.
 
            *NOTE: This only allows you to create private endpoints, not set up the private network as part of the deployment. Users are responsible for setting up their own private networks. Make sure all resources are deployed under the same subscription and same resource group.*

    - `existingFhirId`: 
    
        - Decide whether to use an existing FHIR service or create a new one.
        - Leaving this parameter empty will create a new FHIR service. To use an existing FHIR service, input the FHIR instance ID. Steps to retrieve the FHIR instance ID: 
            1. Navigate to your FHIR service in Azure Portal.
            2. Click on properties in the left menu.
            3. Copy the "Id" field under the "Essentials" group.    

        >*[!IMPORTANT]  
        If you are using an existing FHIR server, please be aware that during deployment, the FHIR server Audience URL will be updated to reflect the new Application Registration ID URL. You will need to update any downstream applications that were using the old FHIR server Audience URL to point to the new URL.*

    - `existingResourceGroupName` : 
    
        - Choose whether to deploy the sample in an existing resource group or create a new one.
        - Leaving this parameter empty will create a new resource group named {env_name}-rg.
        - If you provide an existing resource group name, ensure it does not already contain a SMART on FHIR resources, as multiple samples in the same resource group are not supported.

            *Note:- If you plan to use an existing FHIR service for deployment, enter the name of the resource group where the FHIR service is located. The SMART on FHIR deployment must be in the same resource group as the FHIR service.*
        
*NOTE: The deployment for Virtual Network supported environment will take approximately 60 minutes. While the deployment for Non-Virtual Network supported environment will take approximately 20 minutes. You can proceed with the setup steps outlined below once the deployment is complete. All resources will be deployed to the resource group named {env_name}-rg by default. If you provide an existing resource group name, the resources will be deployed to that group instead.*


## 3. Complete Setup of FHIR Resource and Auth Context Frontend Applications

### Set the Auth User Input Redirect URL

1. Open the resource group named as {env_name}-rg, or with the name of the existing resource group you specified. Find the Azure API Management instance.
1. Copy the Gateway URL for the API Management instance.
1. Open your Application Registration for the Auth Context Frontend you created before deployment. 
1. The Application Registration already contains a redirect URI `http://localhost:3000` that you can use for local debugging. You should add a new redirect URI in the format `<gatewayURL>/auth/context/` as a single-page application redirect URI. Make sure to include the trailing slash.

    - For example: `https://myenv-apim.azure-api.net/auth/context/`

<br />
<details>
<summary>Click to expand and see screenshots.</summary>

![](./images/deployment/4_save_redirect_uri_external_entra_id.png)
</details>
<br />

## 4. Add sample data and US Core resources

To successfully run this sample using Insomnia or POSTMAN, both the US Core FHIR package and applicable data need to be loaded. 


To efficiently load the required data into your FHIR Service, ensure that the user account you are using to execute the script has the **FHIR Data Contributor** role assigned to the FHIR Service. Once confirmed, run the following script:

**For SMART on FHIR with External Entra ID:** 

To run the script given below, you need to pass the FHIR Server Audience parameter. To get the FHIR Server Audience, follow these steps:
- Open the resource group named as {env_name}-rg, or with the name of the existing resource group you specified. Find the FHIR Service instance.
- Navigate to `Settings`  -> `Authentication`.
- Copy the url value present for `Audience` field.

*Note:- Do not copy the Audience value present inside Application1.*

Windows:
```powershell
powershell ./scripts/Load-ProfilesData.ps1 -FhirAudience "<FHIR Server Audience>"
```

Mac/Linux:
```bash
pwsh ./scripts/Load-ProfilesData.ps1 -FhirAudience "<FHIR Server Audience>"
```

To learn more about the sample data, read [sample data](./sample-data.md).

## 5. Mapping test users

**Add `fhirUser` Claim to Test Users:**

-  To properly integrate with the sample data, you need to add the fhirUser claim to each of your test user accounts:
   - For the patient test user, set the `fhirUser` claim to `<Complete Fhir Url without /metadata>/Patient/PatientA`.
   - For the practitioner test user, set the `fhirUser` claim to `<Complete Fhir Url without /metadata>/Practitioner/PractitionerC1`.

   Modifying Microsoft Graph directory extensions requires API requests to Microsoft Graph. Use the command below to set the `fhirUser` claim via a helper script for your patient test user. You will need the `object id` of your patient test user. In a production environment, integrate this step into your user registration process.

    *Note - Log in to your External Entra ID tenant before running the script. Refer to step [2.2](#2-prepare-and-deploy-environment) for instructions on logging into your External Entra ID tenant.*

    Create a Microsoft Graph Directory Extension to hold the `fhirUser` information for users.
    
    Windows:
    ```powershell
    powershell ./scripts/Add-FhirUserInfoToUser.ps1 -ApplicationId "<B2C_EXTENSION_APP_ID>" -UserObjectId "<Patient Object Id>" -FhirUserValue "<Complete Fhir Url without /metadata>/Patient/PatientA"
    ```

    Mac/Linux:
    ```bash
    pwsh ./scripts/Add-FhirUserInfoToUser.ps1 -ApplicationId "<B2C_EXTENSION_APP_ID>" -UserObjectId "<Patient Object Id>" -FhirUserValue "<Complete Fhir Url without /metadata>/Patient/PatientA"
    ```

## 6. Use Insomnia or Postman to access FHIR resource via SMART on FHIR sample

Follow the directions on the [Access SMART on FHIR Using Insomnia or Postman Page](./postman/configure-postman.md) for instructions to access FHIR resources via SMART on FHIR using postman.

## 7. Identity Provider Configuration

**For SMART on FHIR with External Entra ID:** 

To set up SMART on FHIR with External Entra ID, you need to provide the Application Registration details from the External Entra ID tenant. Specifically, you will need to provide the Application Registration ID and Secret. This allows Third Party IDP Support for FHIR Service as well as resources deployed in the Azure AD tenant to access and interact with the Application Registration created in the External Entra ID tenant. Note that resources in the Azure AD tenant cannot directly access the External Entra ID Application Registration without these details.

- Configure Identity Provider:
    1. Open the FHIR Service from the {env_name}-rg resource group, or with the name of the existing resource group you specified.
    1. Select `Settings` -> `Authentication`
    1. In `Identity Provider 1`, within `Application 1`, enter the Client ID from the Application Registration into the `Client ID` field.
    1. Click Save.

    *Note: It would take around 10 minutes to update*
    <details>
    <summary>Click to expand and see screenshots for Reference.</summary>
    
    ![](./images/deployment/8_Identity_Provider_Configuration.png)
    </details>
    <br />
- Create Secrets in KeyVault:
    1. Open the KeyVault from the {env_name}-rg resource group, or with the name of the existing resource group you specified. The Key Vault will have a suffix of `kv`.
    1. Add a new secret to store Client ID and Client Secret of Application Registration.
        - Name: `ExternalAppClientID`
        - Secret: Client ID of Application Registration added earlier in the FHIR Service Authentication.
        - Name: `ExternalAppClientSecret`
        - Secret: Secret generated for this App Registration.
    
    *Note: If the secrets already exist then create a new version of the secret.*

**[Back to Previous Page](../README.md)**