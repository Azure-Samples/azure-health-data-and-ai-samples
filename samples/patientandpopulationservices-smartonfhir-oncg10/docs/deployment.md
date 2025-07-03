> [!TIP]
> *If you encounter any issues during configuration, deployment, or testing, please refer to the [Trouble Shooting Document](./troubleshooting.md)*

# Sample Deployment: Azure Health Data Services SMART on FHIR & ONC (g)(10)

This document guides you through the steps needed to deploy this sample. This sample deploys Azure components, custom code, and Microsoft Entra ID configuration.

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
  -   Access to an Azure Subscription with Owner privileges and Microsoft Entra ID Global Administrator privileges.
  - Elevated access in Microsoft Graph and Microsoft Entra ID to create Application Registrations, assign Microsoft Entra ID roles, and add custom data to user accounts.

- **Test Accounts:**
  - Microsoft Entra ID test account to represent Patient persona. 
  - Microsoft Entra ID test account to represent Provider persona. 
  
Refer the [link](https://learn.microsoft.com/en-us/entra/fundamentals/how-to-create-delete-users#create-a-new-user) for steps. Make sure you have the object id of the user from Microsoft Entra ID.

## 2. Prepare and deploy environment

Next you will need to clone this repository and prepare your environment for deployment by creating two required Azure App Registrations and configuring your environment to use them.

1. Use the terminal or your git client to clone this repo. Open a terminal to the `patientandpopulationservices-smartonfhir-oncg10` folder.
1. Login with the Azure Developer CLI. Specify the tenant if you have more than one. `azd auth login` or `azd auth login --tenant-id <tenant-id>`. Also login with the Azure CLI using `az login`.
1. Run `azd env new` to create a new deployment environment, keeping below points in mind.
    - Environment name must not exceed 18 characters in length.
    - Deployment fails if Environment name contains UpperCase Letters.
    - Use numbers and lower-case letters only for Environment name.
    - Environment name will be the prefix for all of your resources.
1. [Create the FHIR Resource App Registration. Use the instructions here](./ad-apps/fhir-resource-app-registration.md). Record the application id for later.   
1. [Create the Auth Context Frontend App Registration. Use the instructions here](./ad-apps/auth-context-frontend-app-registration.md). Record the application id for later.   
1. Set your deployment environment configuration.
    ```
    azd env set ApiPublisherName "Your Name"
    azd env set ApiPublisherEmail "Your Email"
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
        
*NOTE: The deployment will take approximately 15 minutes. You can proceed with the setup steps outlined below once the deployment is complete. All resources will be deployed to the resource group named {env_name}-rg by default. If you provide an existing resource group name, the resources will be deployed to that group instead.* 

## 3. Complete Setup of FHIR Resource and Auth Context Frontend Applications

### Assign Microsoft Entra ID Permissions for the Auth Custom Operation API

As part of the scope selection flow, the Auth Custom Operation Azure Function will modify user permissions for the signed in user. This requires granting the Azure Managed Identity behind Azure Functions Application Administrator (or similar access).

1. Open the Azure Function for SMART Auth Custom Operations from the {env_name}-rg resource group, or use the name of the existing resource group you specified. The function will have a suffix of `aad-func`. 
1. From the left navbar open `Identity` -> `System assigned`. Copy the Object(principal) ID for the next steps.
1. Open Microsoft Entra ID and navigate to `Roles and Administrators`. Open the `Application Administrator` role.
1. Add the Azure Function Managed Identity to this Microsoft Entra ID role.

<br />
<details>
<summary>Click to expand and see screenshots.</summary>

![](./images/deployment/4_copy_function_managed_identity.png)
![](./images/deployment/4_open_application_administrator.png)
![](./images/deployment/4_assign_function_application_administrator.png)
</details>
<br />

### Set the Auth User Input Redirect URL

1. Open the resource group named as {env_name}-rg, or with the name of the existing resource group you specified. Find the Azure API Management instance.
1. Copy the Gateway URL for the API Management instance.
1. Open your Application Registration for the Auth Context Frontend you created before deployment. 
1. The Application Registration already contains a redirect URI `http://localhost:3000` that you can use for local debugging. You should add a new redirect URI in the format `<gatewayURL>/auth/context/` as a single-page application redirect URI. Make sure to include the trailing slash.

    - For example: `https://myenv-apim.azure-api.net/auth/context/`

<br />
<details>
<summary>Click to expand and see screenshots.</summary>

![](./images/deployment/4_save_redirect_uri.png)
</details>
<br />

## 4. Add sample data and US Core resources

To successfully run the Inferno ONC (g)(10) test suite, both the US Core FHIR package and applicable data need to be loaded. 

To quickly load the needed data to your FHIR Service, make sure your user account has FHIR Data Contributor role on the FHIR Service. Then execute this script:

Windows:
```powershell
powershell ./scripts/Load-ProfilesData.ps1
```

Mac/Linux:
```bash
pwsh ./scripts/Load-ProfilesData.ps1
```

## 5. Mapping test users

**Add `fhirUser` Claim to Test Users:**

-  To properly integrate with the sample data, you need to add the fhirUser claim to each of your test user accounts:
   - For the patient test user, set the `fhirUser` claim to `Patient/PatientA`.
   - For the practitioner test user, set the `fhirUser` claim to `Practitioner/PractitionerC1`.

   Modifying Microsoft Graph directory extensions requires API requests to Microsoft Graph. Use the command below to set the `fhirUser` claim via a helper script for your patient test user. You will need the `object id` of your patient test user. In a production environment, integrate this step into your user registration process.

    Create a Microsoft Graph Directory Extension to hold the `fhirUser` information for users.
    
    Windows:
    ```powershell
    powershell ./scripts/Add-FhirUserInfoToUser.ps1 -UserObjectId "<Patient Object Id>" -FhirUserValue "Patient/PatientA"
    ```

    Mac/Linux:
    ```bash
    pwsh ./scripts/Add-FhirUserInfoToUser.ps1 -UserObjectId "<Patient Object Id>" -FhirUserValue "Patient/PatientA"
    ```
    
**Assign `FHIR SMART User` Role:**

- Make sure your test user has the role `FHIR SMART User` assigned to your FHIR Service deployed as part of this sample.
- This role is necessary for enabling the SMART scope logic with your access token scopes in the FHIR Service.

## 6. Use Postman to access FHIR resource via SMART on FHIR sample

Follow the directions on the [Access SMART on FHIR Using Postman Page](../../smartonfhir/docs//postman/configure-postman.md) for instructions to access FHIR resources via SMART on FHIR using postman.

## 7. Create Inferno Test Applications in Microsoft Entra ID

We will need to create four separate Microsoft Entra ID Applications to run the Inferno (g)(10) test suite. It's best practice to register an Azure Application for each client application that will need to access your FHIR Service. This will allow for granular control of data access per application for the tenant administrator and the users. For more information about best practices for Microsoft Entra ID applications, [read this](https://learn.microsoft.com/en-us/entra/identity-platform/security-best-practices-for-app-registration).

Follow the directions on the [Inferno Test App Registration Page](./ad-apps/inferno-test-app-registration.md) for instructions on registering the needed Azure Applications for the Inferno (g)(10) tests.
- Standalone Patient App (Confidential Client)
- EHR Practitioner App (Confidential Client)
- Backend Service Client
- Standalone Patient App (Public Client)

## 8. Testing Backend Service flow manually

The backend service flow in SMART on FHIR is designed for system-to-system communication, where no user interaction is required. It uses the **client credentials grant** to authorize a backend client (such as a service or scheduled job) to access FHIR resources securely. In this implementation, we register a confidential client, generate a signed JWT (client assertion) using a private key, and exchange it for an access token. This token is then used to interact with protected FHIR APIs such as performing a bulk data export through Azure API Management.

Follow the [SMART on FHIR Backend Service Setup and Manual Testing](./ad-apps/backend-service-client.md) document to test backend service flow manually.