# FHIR Resource App Registration

This application registration is used to customize the access token sent to the FHIR Service. The SMART on FHIR logic inside Azure Health Data Services relies on the `fhirUser` claim inside the access token to restrict user access to their own compartment (e.g. patient can access their own data but not others). Microsoft is unable to allow custom claims mapping on the first-party Healthcare APIs application as it creates a [security hole for malicious applications](https://learn.microsoft.com/azure/active-directory/develop/reference-app-manifest#acceptmappedclaims-attribute). We must then create a custom application registration to protect the FHIR Service and change the audience in the FHIR Service to validate tokens against the custom application.

## Deployment (manual)

1. If you have opted for Microsoft Entra ID, create a FHIR Resource Application Registration in the Microsoft Entra ID tenant. Otherwise, for B2C, create it in the B2C tenant.
    - Go to `App Registrations`
    - Create a new application. It's easiest if this matches the name of your Azure Developer CLI environment.
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
1. This step should only be carried out if you choose Microsoft Entra ID. For Smart on FHIR implementation with B2C, you can skip the below command.
   
   Create a Microsoft Graph Directory Extension to hold the `fhirUser` information for users.
    
    Windows:
    ```powershell
    powershell ./scripts/Create-FhirUserDirectoryExtension.ps1
    ```
    
    Mac/Linux
    ```bash
    pwsh ./scripts/Create-FhirUserDirectoryExtension.ps1
    ```
1. Follow the **Configure fhirUser mapping to token** section in [this page](./set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the access token.

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