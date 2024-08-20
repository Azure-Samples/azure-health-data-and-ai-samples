## Patient Application Registration

1. Follow this [document](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app?tabs=certificate) to create a new application in Microsoft Entra ID. Make sure to select `Web` as the platform and add the redirect URL for Postman (`https://oauth.pstmn.io/v1/callback`).
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
    - Microsoft Graph (Application) - Applicable only for B2C.
        - Application.Read.All
        - DelegatedPermissionGrant.ReadWrite.All 
1. If you have opted for Smart on FHIR with B2C then Grant admin consent for app permissions.
1. Generate a secret for this application. Save this and the client id for testing SMART on FHIR using Postman.
1. If you have opted for Smart on FHIR with B2C, you will need to update the Identity Provider settings. Please refer to [Step 6](./deployment.md/#6-identity-provider-configuration-for-smart-on-fhir-with-b2c) in the deployment document for instructions on how to do this.


## Practitioner Application Registration

1. Follow this [document](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app?tabs=certificate) to create a new application in Microsoft Entra ID. Make sure to select `Web` as the platform and add the redirect URL for Inferno (`https://oauth.pstmn.io/v1/callback`).
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
    - Microsoft Graph (Application) - Applicable only for B2C.
        - Application.Read.All
        - DelegatedPermissionGrant.ReadWrite.All 
1. If you have opted for Smart on FHIR with B2C then Grant admin consent for app permissions.
1. Generate a secret for this application. Save this and the client id for testing SMART on FHIR using Postman.
1. If you have opted for Smart on FHIR with B2C, you will need to update the Identity Provider settings. Please refer to [Step 6](./deployment.md/#6-identity-provider-configuration-for-smart-on-fhir-with-b2c) in the deployment document for instructions on how to do this.