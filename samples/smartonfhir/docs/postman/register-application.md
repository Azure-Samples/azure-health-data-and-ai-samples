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
        - patient.AllergyIntolerance.read
        - patient.CarePlan.read
        - patient.CareTeam.read
        - patient.Condition.read
        - patient.Device.read
        - patient.DiagnosticReport.read
        - patient.DocumentReference.read
        - patient.Encounter.read
        - patient.Goal.read
        - patient.Immunization.read
        - patient.Location.read
        - patient.MedicationRequest.read
        - patient.Medication.read
        - patient.Observation.read
        - patient.Organization.read
        - patient.Patient.read
        - patient.Practitioner.read
        - patient.PractitionerRole.read
        - patient.Procedure.read
        - patient.Provenance.read
    - Microsoft Graph (Delegated)
        - openid
        - offline_access
    - Microsoft Graph (Application) - Applicable only for B2C/Entra External ID.
        - Application.Read.All
        - DelegatedPermissionGrant.ReadWrite.All 
1. If you have opted for Azure AD B2C/Entra External ID then Grant admin consent for app permissions.
1. Generate a secret for this application. Save this and the client id for testing SMART on FHIR using Postman.
1. If you have opted for Microsoft Entra ID/Microsoft Entra External ID, then follow all instructions on [this page](../ad-apps/set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.
1. If you have opted for Azure AD B2C/Entra External ID, you will need to update the Identity Provider settings. Please refer to [Step 7](../deployment.md/#7-identity-provider-configuration) in the deployment document for instructions on how to do this.


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
        - user.AllergyIntolerance.read
        - user.CarePlan.read
        - user.CareTeam.read
        - user.Condition.read
        - user.Device.read
        - user.DiagnosticReport.read
        - user.DocumentReference.read
        - user.Encounter.read
        - user.Goal.read
        - user.Immunization.read
        - user.Location.read
        - user.MedicationRequest.read
        - user.Medication.read
        - user.Observation.read
        - user.Organization.read
        - user.Patient.read
        - user.Practitioner.read
        - user.PractitionerRole.read
        - user.Procedure.read
        - user.Provenance.read
    - Microsoft Graph (Delegated)
        - openid
        - offline_access
    - Microsoft Graph (Application) - Applicable only for B2C/Entra External ID.
        - Application.Read.All
        - DelegatedPermissionGrant.ReadWrite.All 
1. If you have opted for Azure AD B2C/Microsoft Entra External ID then Grant admin consent for app permissions.
1. Generate a secret for this application. Save this and the client id for testing SMART on FHIR using Postman.
1. If you have opted for Microsoft Entra ID/Microsoft Entra External ID, then follow all instructions on [this page](../ad-apps/set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.
1. If you have opted for Azure AD B2C/Microsoft Entra External ID, you will need to update the Identity Provider settings. Please refer to [Step 7](../deployment.md/#7-identity-provider-configuration) in the deployment document for instructions on how to do this.

**[Back to Previous Page](./configure-postman.md)**