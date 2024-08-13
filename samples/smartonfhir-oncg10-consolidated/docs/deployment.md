# Sample Deployment: Azure Health Data Services SMART on FHIR & ONC (g)(10)

This document guides you through the steps needed to deploy this sample. This sample deploys Azure components, custom code, and Microsoft Entra ID or Azure AD B2C configuration.

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
  - [PowerShell Version 5.1.22621.2428 or Greater](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) installed for running scripts (works for Mac and Linux too!).

- **Access:**

    The access requirements depend on the Identity Provider you choose:

    - **For Microsoft Entra ID:**
        -   Access to an Azure Subscription with Owner privileges and Microsoft Entra ID Global Administrator privileges.
        - Elevated access in Microsoft Graph and Microsoft Entra ID to create Application Registrations, assign Microsoft Entra ID roles, and add custom data to user accounts.

    -   **For Azure B2C:**
        - If an Azure AD B2C tenant has not yet been created, please use the following link to set one up.
        [Deploy an Azure AD B2C tenant by using an ARM template.](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/azure-ad-b2c-setup?branch=main&branchFallbackFrom=pr-en-us-261649&tabs=powershell#deploy-an-azure-ad-b2c-tenant-by-using-an-arm-template)
        - Need to have admin access to an Azure B2C to create application registration, role assignments, create custom policies, create user accounts.

- **Test User Accounts:**

    To effectively test the application, you need to create two test user accounts: one for the Patient persona and another for the Provider persona. You can choose to create these user accounts in your choosen Identity Provider

    -   **For Microsoft Entra ID:**
        - Create two new test user accounts: one for the Patient persona and one for the Provider persona. Refer the [link](https://learn.microsoft.com/en-us/entra/fundamentals/how-to-create-delete-users#create-a-new-user) for steps.
        - Make sure you have the object id of both the user accounts from Microsoft Entra ID.

    -   **For Azure B2C:**
        - Create two test user accounts: one for the Patient persona and one for the Provider persona. Refer [Add a test B2C user to the Azure AD B2C tenant](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/azure-ad-b2c-setup?branch=main&branchFallbackFrom=pr-en-us-261649&tabs=powershell#add-a-test-b2c-user-to-the-azure-ad-b2c-tenant)
        - Make sure you have the object id of both the accounts/users from Azure B2C.
  

- **Azure B2C SetUp:**
  - This setup is exclusively necessary for Smart on FHIR implementation with B2C. If you opt for Microsoft Entra ID, you can bypass this configuration.
  - Follow below mentioned steps:
    - [Create the custom user attribute in B2C tenant.](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/azure-ad-b2c-setup?branch=main&branchFallbackFrom=pr-en-us-261649&tabs=powershell#link-a-b2c-user-with-the-fhiruser-custom-user-attribute) Refer only to the **Link a B2C user with the fhirUser custom user attribute** section.
    - Refer [Create custom user flow using custom policy](../docs/create-custom-policy.md) section to create custom user attribute.

## 2. Prepare and deploy environment

Next you will need to clone this repository and prepare your environment for deployment by creating required Azure App Registrations and configuring your environment to use them.

1. Use the terminal or your git client to clone this repo. Open a terminal to the `smartonfhir-oncg10-consolidated` folder.
1. Login with the Azure CLI.
   - **For Microsoft Entra ID:** 
        ```
        az login --tenant <tenant-id>
        azd auth login --tenant-id <tenant-id>
        ```

   - **For Azure B2C:**
        ```
        az login --tenant <B2CTenantDomainName> --allow-no-subscriptions
        ```

1. Run `azd env new` to create a new deployment environment, keeping below points in mind.
    - Environment name must not exceed 18 characters in length.
    - Deployment fails if Environment name contains UpperCase Letters.
    - Use numbers and lower-case letters only for Environment name.
    - Environment name will be the prefix for all of your resources.

1. [Create the FHIR Resource App Registration. Use the instructions here](./ad-apps/fhir-resource-app-registration.md). Record the application id and application url for later.

1. [Create the Auth Context Frontend App Registration. Use the instructions here](./ad-apps/auth-context-frontend-app-registration.md). Record the application id and application url for later.   

1. If you are using B2C, the client application must be created in the B2C tenant before beginning the sample deployment. You will need the Application ID for the deployment process. Refer to the [Create Inferno Standalone Patient App](./ad-apps/inferno-test-app-registration.md) section for detailed instructions on how to create the application. If you are using Entra ID, this step is not required at this time.

1. Set the deployment environment configuration as below
    - **Common configurations for all IDPs** (*Irrespective of IDP choosen*)
        ```
        azd env set ApiPublisherName "Your Name"
        azd env set ApiPublisherEmail "Your Email"
        ```
    - **For Microsoft Entra ID**
        ```
        azd env set AuthorityURL "https://login.microsoftonline.com/<Microsoft Entra ID Tenant Id>/v2.0" 
        azd env set SmartonFhirwithB2C false
        ```
    - **For Azure B2C**
        ```
        azd env set B2CTenantId <Tenant_ID_Of_B2C>
        azd env set AuthorityURL "https://<YOUR_B2C_TENANT_NAME>.b2clogin.com/<YOUR_B2C_TENANT_NAME>.onmicrosoft.com/<YOUR_USER_FLOW_NAME>/v2.0"
        azd env set StandaloneAppClientId <STANDALONE_APP_ID_CREATED_IN_STEP_6>
        azd env set SmartonFhirwithB2C true
        ```
1. To enable ONC g(10) compliance for this sample, set the `oncEnabled` variable to true; if not needed, set it to false. Enabling this setting will grant access to additional resources necessary for ONC g(10) compliance.
    ```
    azd env set oncEnabled <true/false>
    ```
1. To begin the sample deployment, you need to be logged into the appropriate tenant.

    - **For Microsoft Entra ID**: You already completed this in step 1, so you can skip this step.

    - **For Azure B2C**: Although you logged into the B2C tenant in step 1, you still need to log in to the Azure tenant using below commands. 
        ```
        az login --tenant <tenant-id>
        azd auth login --tenant-id <tenant-id>
        ```
1. Initiate the environment deployment by executing the `azd` command. This will handle both the infrastructure provisioning and code deployment, which should take around one hour to complete.

    *Note:- When executing the `azd up` command, you will be asked to provide several values. Below, you will find a detailed explanation of each prompt.*

    ```
    azd up
    ```

    **Deployment Instructions**

    When running the `azd up` command, you will need to select the `subscription name` and `location` from the drop-down menus to specify where to deploy all resources. Note that this sample can only be deployed in the `EastUS2, WestUS2, or CentralUS` regions. Ensure you choose one of these regions during deployment.

    The `azd up` command will prompt you to enter values for the following parameters:   
    
    - `B2CTenantId` : 
        - Enter the Tenant ID of your B2C Tenant deployed earlier. (*If you have opted for Microsoft Entra ID you can keep this parameter blank.*)
        
    - `StandaloneAppClientId` : 
        - Enter the Application ID / Client ID of the Inferno Standalone Patient App created earlier. (*If you have opted for Microsoft Entra ID you can keep this parameter blank.*)            
    
    - `existingResourceGroupName` : 
    
        - Choose whether to deploy the sample in an existing resource group or create a new one.
        - Leaving this parameter empty will create a new resource group named {env_name}-rg.
        - If you provide an existing resource group name, ensure it does not already contain a SMART on FHIR resources, as multiple samples in the same resource group are not supported.

            *Note:- If you plan to use an existing FHIR service for deployment, enter the name of the resource group where the FHIR service is located. The SMART on FHIR deployment must be in the same resource group as the FHIR service. *
    
    - `exitingFhirId`: 
    
        - Decide whether to use an existing FHIR service or create a new one.
        - Leaving this parameter empty will create a new FHIR service. To use an existing FHIR service, input the FHIR instance ID. Steps to retrieve the FHIR instance ID: 
            1. Navigate to your FHIR service in Azure Portal.
            2. Click on properties in the left menu.
            3. Copy the "Id" field under the "Essentials" group.    

        >*[!IMPORTANT]  
        If you are using an existing FHIR server, please be aware that during deployment, the FHIR server Audience URL will be updated to reflect the new Application Registration ID URL. You will need to update any downstream applications that were using the old FHIR server Audience URL to point to the new URL.*
       
     - `enableVNetSupport`: 
     
        - This parameter accepts a boolean (true/false) value.
 
        - When set to false, the follwing resorces are deployed with mentioned configurations. User will not be able to create private endpoints and will not be able to setup private network.
            1. API Management (APIM): Deployed in the Consumption tier.
            2. App Service Plan : Deployed in the Dynamic tier. 
            3. Static Web App: Deployed in the Free tier.
            4. Function Apps and App Service Plan: Utilizes Linux as the operating system.
        
        - When set to true, the following resources are deployed in the Standard/Premium tier to enable private endpoint creation necessary for Virtual Network Support. 
            1. API Management (APIM): Deployed in the Premium tier.
            2. App Service Plan and Static Web App: Deployed in the Standard tier.
            3. Function Apps and App Service Plan: Utilizes Windows as the operating system.
 
            *NOTE: This only allows you to create private endpoints, not set up the private network as part of the deployment. Users are responsible for setting up their own private networks. Make sure all resources are deployed under the same subscription and same resource group.*
        
*NOTE: The deployment will take approximately 15 minutes. You can proceed with the setup steps outlined below once the deployment is complete. All resources will be deployed to the resource group named {env_name}-rg by default. If you provide an existing resource group name, the resources will be deployed to that group instead.*

## 3. Complete Setup of FHIR Resource and Auth Context Frontend Applications
### Assign Permissions for the Auth Custom Operation API

As part of the scope selection process, the Auth Custom Operation Azure Function modifies user permissions for the signed-in user.

- **For Microsoft Entra ID:** 
  - You will need to grant the Azure Managed Identity associated with the Azure Function appropriate permissions, such as the Application Administrator role (or similar).

    1. Open the Azure Function for the SMART Auth Custom Operations from the {env_name}-rg resource group. The function will have a suffix of `aad-func`. 
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

- **For Azure B2C:**
  - You will need to access the applications registered in B2C tenant for the SMART Auth Custom Operations. You need to provide client secret of Standalone application in key vault. 

    1. In the resource group that matches your environment, open the KeyVault with the suffix `-kv`.
    1. Add a new secret that corresponds to the Standalone Application you just generated.
        - Name: `standalone-app-secret`
        - Secret: The secret you generated for the Standalone application


### Set the Auth User Input Redirect URL

1. Open the resource group created by deployment. Find the Azure API Management instance.
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

*NOTE: Changes made to Application Registration in Azure B2C Tenant takes time to reflect.*

## 4. Add sample data and US Core resources

To successfully run this sample using POSTMAN or with Inferno ONC (g)(10) test suite, both the US Core FHIR package and applicable data need to be loaded. 

To efficiently load the required data into your FHIR Service, ensure that the user account you are using to execute the script has the **FHIR Data Contributor** role assigned to the FHIR Service. Once confirmed, run the following script:

Windows:
```powershell
powershell ./scripts/Load-ProfilesData.ps1
```

Mac/Linux:
```bash
pwsh ./scripts/Load-ProfilesData.ps1
```

To learn more about the sample data, read [sample data](./sample-data.md).

## 5. Mapping test users

**Add `fhirUser` Claim to Test Users:**

-  To properly integrate with the sample data, you need to add the fhirUser claim to each of your test user accounts:
   - For the patient test user, set the `fhirUser` claim to `<Complete Fhir Url>/Patient/PatientA`.
   - For the practitioner test user, set the `fhirUser` claim to `<Complete Fhir Url>/Practitioner/PractitionerC1`.

   Modifying Microsoft Graph directory extensions requires API requests to Microsoft Graph. Use the command below to set the `fhirUser` claim via a helper script for your patient test user. You will need the `object id` of your patient test user. In a production environment, integrate this step into your user registration process.

    *Note - If you have chosen Smart on FHIR with B2C, log in to your B2C tenant before running the script. Refer to step [2.2](#2-prepare-and-deploy-environment) for instructions on logging into your B2C tenant.*

    Create a Microsoft Graph Directory Extension to hold the `fhirUser` information for users.
    
    Windows:
    ```powershell
    powershell ./scripts/Add-FhirUserInfoToUser.ps1 -ApplicationId "<If you opted for B2C pass B2C_EXTENSION_APP_ID otherwise for Microsoft Entra ID pass Fhir Resource Application Id>" -UserObjectId "<Patient Object Id>" -FhirUserValue "<Complete Fhir Url>/Patient/PatientA"
    ```

    Mac/Linux:
    ```bash
    pwsh ./scripts/Add-FhirUserInfoToUser.ps1 -ApplicationId "<If you opted for B2C pass B2C_EXTENSION_APP_ID otherwise for Microsoft Entra ID pass Fhir Resource Application Id>" -UserObjectId "<Patient Object Id>" -FhirUserValue "<Complete Fhir Url>/Patient/PatientA"
    ```
    
**Assign `FHIR SMART User` Role:**

- If you have opted for Microsoft Entra ID, then make sure your test user has the role `FHIR SMART User` assigned to your FHIR Service deployed as part of this sample.
- This role is necessary for enabling the SMART scope logic with your access token scopes in the FHIR Service.

## 6. Use Postman to access FHIR resource via SMART on FHIR sample

Follow the directions on the [Access SMART on FHIR Using Postman Page](./postman/configure-postman.md) for instructions to access FHIR resources via SMART on FHIR using postman.

## 7. Create Inferno Test Applications

*Note: This section applies if the sample is deployed to be compliant with ONC (g)(10).*

To run the Inferno (g)(10) test suite, four separate Microsoft Entra ID/B2C Applications will need to be created. It is a best practice to register an individual Azure Application for each client application that will access your FHIR Service. This approach enables granular control over data access for each application, providing tenant administrators and users with enhanced security and management options.
For more information about best practices on creating applications, refer to the documentation for [Microsoft Entra ID](https://learn.microsoft.com/en-us/entra/identity-platform/security-best-practices-for-app-registration) or [Azure Active Directory B2C](https://learn.microsoft.com/en-us/azure/active-directory-b2c/tutorial-register-applications), depending on the IDP you selected for sample deployment.

Follow the directions on the [Inferno Test App Registration Page](./ad-apps/inferno-test-app-registration.md) for instructions on registering the needed Azure Applications for the Inferno (g)(10) tests.
- Standalone Patient App (Confidential Client)
- EHR Practitioner App (Confidential Client)
- Backend Service Client (*Not supported for Azure B2C*)
- Standalone Patient App (Public Client)


