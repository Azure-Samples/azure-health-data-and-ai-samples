> [!TIP]
> *If you encounter any issues during configuration, deployment, or testing, please refer to the [Trouble Shooting Document](../troubleshooting.md)*

# FHIR Resource App Registration

This application registration is used to customize the access token sent to the FHIR Service or Azure API for FHIR. The SMART on FHIR logic inside Azure Health Data Services or Azure API for FHIR relies on the `fhirUser` claim inside the access token to restrict user access to their own compartment (e.g. patient can access their own data but not others). Microsoft is unable to allow custom claims mapping on the first-party Healthcare APIs application as it creates a [security hole for malicious applications](https://learn.microsoft.com/azure/active-directory/develop/reference-app-manifest#acceptmappedclaims-attribute). We must then create a custom application registration to protect the FHIR Service or Azure API for FHIR and change the audience in the FHIR Service or Azure API for FHIR to validate tokens against the custom application.

## Deployment (manual)

1. If you have chosen Microsoft Entra ID, register a FHIR Resource Application in your Microsoft Entra ID tenant. If you have chosen Azure B2C, register the application in your B2C tenant instead.
    - Go to `App Registrations`
    - Create a new application. You can name the App Registration to either match your Azure Developer CLI environment name or use a custom name. 
        - If you choose a custom name, make sure it does not contain any whitespace. 
        - For reference on the environment name, see step [2.3](../deployment.md/#2-prepare-and-deploy-environment/).
    - Click `Register` (ignore redirect URI).
1. Inform your Azure Developer CLI environment of this application with:
    ```
    azd env set FhirResourceAppId <FHIR Resource App Id>
    ```
1. Run below command to configure a FHIR Resource Application Registration.
    
    Windows:
    ```powershell
    powershell ./scripts/Configure-FhirResourceAppRegistration.ps1
    ```
    
    Mac/Linux
    ```bash
    pwsh ./scripts/Configure-FhirResourceAppRegistration.ps1
    ```
1. This step is required only for Microsoft Entra ID. For SMART on FHIR implementations with B2C, this command can be skipped.

    Create a Microsoft Graph Directory Extension to hold the `fhirUser` information for users.
    
    Windows:
    ```powershell
    powershell ./scripts/Create-FhirUserDirectoryExtension.ps1
    ```
    
    Mac/Linux
    ```bash
    pwsh ./scripts/Create-FhirUserDirectoryExtension.ps1
    ```
1. Follow the steps in **Set fhirUser Claims Mapping** section in [this page](./set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the access token. Note that this is required only for Microsoft Entra ID and not for Azure B2C.

<br />
<details>
<summary>Click to expand and see screenshots for Microsoft Entra ID Reference.</summary>

![](./images/fhir_resource_app_primary_domain.png)
![](./images/fhir_resource_app_new_app.png)
![](./images/fhir_resource_app_new_app2.png)
![](./images/fhir_resource_app_set_uri.png)
![](./images/fhir_resource_app_set_uri2.png)
![](./images/fhir_resource_app_manifest.png)
</details>

<br />
<details>
<summary>Click to expand and see screenshots for B2C Reference.</summary>

![](./images/fhir_resource_app_primary_domain_b2c.png)
![](./images/fhir_resource_app_new_app_b2c.png)
![](./images/fhir_resource_app_new_app2_b2c.png)
![](./images/fhir_resource_app_set_uri_b2c.png)
![](./images/fhir_resource_app_set_uri2_b2c.png)
![](./images/fhir_resource_app_manifest_b2c.png)
</details>

**[Back to Previous Page](../deployment.md#2-prepare-and-deploy-environment)**