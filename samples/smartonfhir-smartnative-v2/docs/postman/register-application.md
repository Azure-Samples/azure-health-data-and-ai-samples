> [!TIP]
> *If you encounter any issues during configuration, deployment, or testing, please refer to the [Trouble Shooting Document](../troubleshooting.md)*

## Patient Application Registration

1. Register your application in the appropriate tenant depending on the Identity Provider you've selected:

    - **Microsoft Entra ID**  
    Follow [this guide](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app?tabs=certificate) to register the application in your Microsoft Entra ID tenant.

    - **Azure AD B2C**  
    Follow [this guide](https://learn.microsoft.com/en-us/azure/active-directory-b2c/tutorial-register-applications) to register the application in your Azure AD B2C tenant.

    - **Microsoft Entra External ID**  
    Follow [this guide](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app) to register the application in your Entra External ID tenant.

    **Note:** For all identity providers, select **`Web`** as the platform and configure the appropriate **redirect URI** for your application. URL for Postman (`https://oauth.pstmn.io/v1/callback`).
1. In API Permissions for this new application, add the below:
    - Your FHIR Resource API (Delegated)
        - fhirUser
        - launch.patient
        - patient.AllergyIntolerance.rs
        - patient.CarePlan.rs
        - patient.CareTeam.rs
        - patient.Condition.rs
        - patient.Device.rs
        - patient.DiagnosticReport.rs
        - patient.DocumentReference.rs
        - patient.Encounter.rs
        - patient.Goal.rs
        - patient.Immunization.rs
        - patient.Location.rs
        - patient.MedicationRequest.rs
        - patient.Medication.rs
        - patient.Observation.rs
        - patient.Organization.rs
        - patient.Patient.rs
        - patient.Practitioner.rs
        - patient.PractitionerRole.rs
        - patient.Procedure.rs
        - patient.Provenance.rs
    - Microsoft Graph (Delegated)
        - openid
        - offline_access
    - Microsoft Graph (Application) - Applicable only for non-Microsoft Entra ID Identity Providers.
        - Application.Read.All
        - DelegatedPermissionGrant.ReadWrite.All 
1. If you have selected a non-Microsoft Entra ID Identity Provider then Grant admin consent for app permissions.
1. Generate a secret for this application. Save this and the client id for testing SMART on FHIR using Postman.
1. If you have selected an Identity Provider other than Azure AD B2C, then follow all instructions on [this page](../ad-apps/set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.
1. If you have selected a non-Microsoft Entra ID Identity Provider, you will need to update the Identity Provider settings. Please refer to [Step 7](../deployment.md/#7-identity-provider-configuration) in the deployment document for instructions on how to do this.


## Practitioner Application Registration

1. Register your application in the appropriate tenant depending on the Identity Provider you've selected:

    - **Microsoft Entra ID**  
    Follow [this guide](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app?tabs=certificate) to register the application in your Microsoft Entra ID tenant.

    - **Azure AD B2C**  
    Follow [this guide](https://learn.microsoft.com/en-us/azure/active-directory-b2c/tutorial-register-applications) to register the application in your Azure AD B2C tenant.

    - **Microsoft Entra External ID**  
    Follow [this guide](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app) to register the application in your Entra External ID tenant.

    **Note:** For all identity providers, select **`Web`** as the platform and configure the appropriate **redirect URI** for your application. URL for Postman (`https://oauth.pstmn.io/v1/callback`).
1. In API Permissions for this new application, add the below:
    - Your FHIR Resource Application (Delegated)
        - fhirUser
        - launch
        - user.AllergyIntolerance.rs
        - user.CarePlan.rs
        - user.CareTeam.rs
        - user.Condition.rs
        - user.Device.rs
        - user.DiagnosticReport.rs
        - user.DocumentReference.rs
        - user.Encounter.rs
        - user.Goal.rs
        - user.Immunization.rs
        - user.Location.rs
        - user.MedicationRequest.rs
        - user.Medication.rs
        - user.Observation.rs
        - user.Organization.rs
        - user.Patient.rs
        - user.Practitioner.rs
        - user.PractitionerRole.rs
        - user.Procedure.rs
        - user.Provenance.rs
    - Microsoft Graph (Delegated)
        - openid
        - offline_access
    - Microsoft Graph (Application) - Applicable only for non-Microsoft Entra ID Identity Providers.
        - Application.Read.All
        - DelegatedPermissionGrant.ReadWrite.All 
1. If you have selected a non-Microsoft Entra ID Identity Provider then Grant admin consent for app permissions.
1. Generate a secret for this application. Save this and the client id for testing SMART on FHIR using Postman.
1. If you have selected an Identity Provider other than Azure AD B2C, then follow all instructions on [this page](../ad-apps/set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.
1. If you have selected a non-Microsoft Entra ID Identity Provider, you will need to update the Identity Provider settings. Please refer to [Step 7](../deployment.md/#7-identity-provider-configuration) in the deployment document for instructions on how to do this.

**[Back to Previous Page](./configure-postman.md)**