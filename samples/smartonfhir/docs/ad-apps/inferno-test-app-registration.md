> [!TIP]
> *If you encounter any issues during configuration, deployment, or testing, please refer to the [Trouble Shooting Document](../troubleshooting.md)*

# Inferno Test Applications 

To successfully test ONC (g)(10) with Inferno, you will need to create four separate application registrations to represent the different access scenarios addressed by this test. This method of setting up application registrations is applicable to real SMART on FHIR applications too.

## Patient Standalone Confidential Client / Public Client Applications

The Patient Standalone Launch application consists of two types of client applications:
1. **Confidential Client (Web):** This application can protect a secret and is used for sections 1 & 2 of the test.
1. **Public Client (SPA):** This application cannot protect a secret and is used for section 9 of the test.

You will need to follow the instructions below twice—once for the confidential client and once for the public client: 

1. If you have opted for Microsoft Entra ID, create a new application registration in the Microsoft Entra ID tenant. Otherwise for B2C, create it in the B2C tenant. Make sure to select platform (Note : Create Confidential Client application with Web platform and Public Client application with SPA platform) and add the redirect URL for Inferno (`https://inferno.healthit.gov/suites/custom/smart/redirect`).
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
1. Generate a secret for this application. Save this secret and the client id for testing
    - *Standalone Patient App*
    - *Public Client App* (if you have opted for Smart on FHIR with B2C)
1. Additional Steps for Public Client App (SPA), If you have opted for Smart on FHIR with B2C:
    - Select `Manage` -> `Authentication`
    - Under the Implicit Grant and Hybrid Flows section, ensure the following options are selected: 
        - `Access tokens (used for implicit flows)`
        - `ID tokens (used for implicit and hybrid flows)`
    - Go to the Advanced Settings section.
    - Set **Allow public client flows** to `Yes`.
1. If you have opted for Smart on FHIR with B2C, you will need to update the Identity Provider settings. Please refer to [Step 7](../deployment.md/#7-identity-provider-configuration) in the deployment document for instructions on how to do this.
1. If you have opted for Microsoft Entra ID, then follow all instructions on [this page](./set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.

<br /><details><summary>Click to expand and see screenshots for Microsoft Entra ID Reference.</summary>
        ![](./images/5_confidential_client_1.png)        
        ![](./images/5_client_confidental_app_scopes.png)
    </details>
<br /><details><summary>Click to expand and see screenshots for B2C Reference.</summary>
        ![](./images/5_confidential_client_1_b2c.png)
        ![](./images/5_client_confidental_app_scopes_b2c.png)
        ![](./images/5_client_public_app.png)
    </details>

## EHR Launch Confidential Client Application

The EHR launch confidential client application is a standard confidential client application which represents an application that can protect a secret (section 3 of the test).

1. If you have opted for Microsoft Entra ID, create a new application registration in the Microsoft Entra ID tenant. Otherwise for B2C, create it in the B2C tenant. Make sure to select `Web` as the platform and add the redirect URL for Inferno (`https://inferno.healthit.gov/suites/custom/smart/redirect`).
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
1. Generate a secret for this application. Save this and the client id for testing Inferno *3. EHR Practitioner App*.
1. If you have opted for Smart on FHIR with B2C, you will need to update the Identity Provider settings. Please refer to [Step 7](../deployment.md/#7-identity-provider-configuration) in the deployment document for instructions on how to do this.
1. If you have opted for Microsoft Entra ID, then Follow all instructions on [this page](./set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.
<br /><details><summary>Click to expand and see screenshots.</summary>
    ![](./images/5_confidential_client_1.png)

    ![](./images/5_ehr_confidental_app_scopes.png)
    </details>

## Inferno Public Service Base URL

This repository contains a sample code to validate conformance to the HTI-1 rule from the API Condition and Maintenance of Certification. The test suite, known as **Service Base URL Test Suite**, ensures that Certified API Developers with patient-facing apps publish their service base URLs and related organizational details in the specified format. Specifically, it checks that the service base URLs are publicly accessible and formatted according to the FHIR 4.0.1 standard, and that the necessary organizational details are correctly referenced and bundled. This sample provides a public endpoint to pass the test suite.

Before executing the test, follow these steps to configure your environment:

1. **Create Secrets in Key Vault**:
    - Open the KeyVault from the {env_name}-rg resource group, or with the name of the existing resource group you specified. The Key Vault will have a suffix of `kv`.
    - Add the following secrets along with their values for the Endpoint resource:
        - `status` = active
        - `connectionType` = http://terminology.hl7.org/CodeSystem/endpoint-connection-type
        - `address` = `{apim-url}/smart`
            - For example: `https://myenv-apim.azure-api.net/smart`
    - Add the following secrets along with their values for the Organization resource:
        - `active` = true
        - `name` = Health Intersections CarePlan Hub
        - `location` = USA
        - `identifier` = http://hl7.org/fhir/sid/us-npi
    - Ensure that the names of the secrets are exactly as provided above with no uppercase letters or alterations.

2. **Sample Endpoint**:
    - Use the following URL `{apim-url}/smart/service-base` to test Service Base URL Test Suite. 
    - Replace `{apim-url}` with your deployed APIM service url in the resource group.

**[Back to Previous Page](../deployment.md#7-create-inferno-test-applications)**