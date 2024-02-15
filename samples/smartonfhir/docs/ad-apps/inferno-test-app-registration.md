# Test Applications 

This method of setting up application registrations is applicable to real SMART on FHIR applications.

## Patient Standalone Confidential Client / Public Client Applications

The Patient Standalone Launch application is a standard confidential client application which represents an application that can protect a secret. The public client represents an application that cannot protect a secret. You will need to follow these instructions for the confidential client. 

1. Create a new application in B2C tenant. Make sure to select platform (Note : You need one application with platform - Web and SPA respectively) and add the redirect URL for Inferno (`https://oauth.pstmn.io/v1/callback`).
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
    - Microsoft Graph (Application)
        - Application.Read.All
        - DelegatedPermissionGrant.Read.All
1. Grant admin consent for app permissions.
1. Generate a secret for this application. Save this secret and the client id for testing *1. Standalone Patient App*.
1. Follow all instructions on [this page](./set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.

<br />
<details>
<summary>Click to expand and see screenshots.</summary>
<br />

## EHR Launch Confidential Client Application

The EHR launch confidential client application is a standard confidential client application which represents an application that can protect a secret (section 3 of the test).

1. Create a new application in Azure Active Directory. Make sure to select `Web` as the platform and add the redirect URL for Inferno (`https://oauth.pstmn.io/v1/callback`).
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
1. Generate a secret for this application. Save this secret and the client id for testing *3. EHR Practitioner App*.
1. Follow all instructions on [this page](./set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.

<br />