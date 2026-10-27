> [!TIP]
> *If you encounter any issues during configuration, deployment, or testing, please refer to the [Trouble Shooting Document](../troubleshooting.md)*

> Note - Throughout this document, the term `FHIR Server` refers to either AHDS FHIR service or Azure API for FHIR, depending on the configuration or user preference.

# Inferno Test Applications 

To successfully test ONC (g)(10) with Inferno, you will need to create four separate application registrations to represent the different access scenarios addressed by this test. This method of setting up application registrations is applicable to real SMART on FHIR applications too.

## Patient Standalone Confidential Client / Public Client Applications

The Patient Standalone Launch application is a standard confidential client application which represents an application that can protect a secret (section 1 & 2 of the test). The public client represents an application that cannot protect a secret (section 9 of the test). You will need to follow these instructions twice - once for the confidential client (web) and once for the public client (spa). 

1. Create a new application in Microsoft Entra ID. Make sure to select platform (Note : You need one application with platform - Web and SPA respectively) and add the redirect URL for Inferno (`https://inferno.healthit.gov/suites/custom/smart/redirect`).
1. In API Permissions for this new application, add the below:
    - Your FHIR Resource API (Delegated)

| Common Scopes        | SMART V2 Scopes                         | SMART V1 Scopes                               | Microsoft Graph Scopes |
|---------------------|-------------------------------------|---------------------------------------------|------------------------|
| `fhirUser`          | `patient.AllergyIntolerance.rs`     | `patient.AllergyIntolerance.read`           | `openid`               |
| `launch.patient`    | `patient.CarePlan.rs`               | `patient.CarePlan.read`                     | `offline_access`       |
|                     | `patient.CareTeam.rs`               | `patient.CareTeam.read`                     |                        |
|                     | `patient.Condition.rs`              | `patient.Condition.read`                    |                        |
|                     | `patient.Device.rs`                 | `patient.Device.read`                       |                        |
|                     | `patient.DiagnosticReport.rs`       | `patient.DiagnosticReport.read`             |                        |
|                     | `patient.DocumentReference.rs`      | `patient.DocumentReference.read`            |                        |
|                     | `patient.Encounter.rs`              | `patient.Encounter.read`                    |                        |
|                     | `patient.Goal.rs`                   | `patient.Goal.read`                         |                        |
|                     | `patient.Immunization.rs`           | `patient.Immunization.read`                 |                        |
|                     | `patient.Location.rs`               | `patient.Location.read`                     |                        |
|                     | `patient.MedicationRequest.rs`      | `patient.MedicationRequest.read`            |                        |
|                     | `patient.Medication.rs`             | `patient.Medication.read`                   |                        |
|                     | `patient.Observation.rs`            | `patient.Observation.read`                  |                        |
|                     | `patient.Organization.rs`           | `patient.Organization.read`                 |                        |
|                     | `patient.Patient.rs`                | `patient.Patient.read`                      |                        |
|                     | `patient.Practitioner.rs`           | `patient.Practitioner.read`                 |                        |
|                     | `patient.PractitionerRole.rs`       | `patient.PractitionerRole.read`             |                        |
|                     | `patient.Procedure.rs`              | `patient.Procedure.read`                    |                        |
|                     | `patient.Provenance.rs`             | `patient.Provenance.read`                   |                        |

1. Generate a secret for this application. Save this and the client id for testing Inferno *1. Standalone Patient App* and *2. Limited Access App*.
1. Follow all instructions on [this page](./set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.


## EHR Launch Confidential Client Application

The EHR launch confidential client application is a standard confidential client application which represents an application that can protect a secret (section 3 of the test).

1. Create a new application in Microsoft Entra ID. Make sure to select `Web` as the platform and add the redirect URL for Inferno (`https://inferno.healthit.gov/suites/custom/smart/redirect`).
1. In API Permissions for this new application, add the below:
    - Your FHIR Resource Application (Delegated)
    
| Common Scopes        | SMART V2 Scopes                         | SMART V1 Scopes                               | Microsoft Graph Scopes |
|---------------------|-------------------------------------|---------------------------------------------|------------------------|
| `fhirUser`          | `user.AllergyIntolerance.rs`        | `user.AllergyIntolerance.read`              | `openid`               |
| `launch`    | `user.CarePlan.rs`                  | `user.CarePlan.read`                        | `offline_access`       |
|                     | `user.CareTeam.rs`                  | `user.CareTeam.read`                        |                        |
|                     | `user.Condition.rs`                 | `user.Condition.read`                       |                        |
|                     | `user.Device.rs`                    | `user.Device.read`                          |                        |
|                     | `user.DiagnosticReport.rs`          | `user.DiagnosticReport.read`                |                        |
|                     | `user.DocumentReference.rs`         | `user.DocumentReference.read`               |                        |
|                     | `user.Encounter.rs`                 | `user.Encounter.read`                       |                        |
|                     | `user.Goal.rs`                      | `user.Goal.read`                            |                        |
|                     | `user.Immunization.rs`              | `user.Immunization.read`                    |                        |
|                     | `user.Location.rs`                  | `user.Location.read`                        |                        |
|                     | `user.MedicationRequest.rs`         | `user.MedicationRequest.read`               |                        |
|                     | `user.Medication.rs`                | `user.Medication.read`                      |                        |
|                     | `user.Observation.rs`               | `user.Observation.read`                     |                        |
|                     | `user.Organization.rs`              | `user.Organization.read`                    |                        |
|                     | `user.Patient.rs`                   | `user.Patient.read`                         |                        |
|                     | `user.Practitioner.rs`              | `user.Practitioner.read`                    |                        |
|                     | `user.PractitionerRole.rs`          | `user.PractitionerRole.read`                |                        |
|                     | `user.Procedure.rs`                 | `user.Procedure.read`                       |                        |
|                     | `user.Provenance.rs`                | `user.Provenance.read`                      |                        |


1. Generate a secret for this application. Save this and the client id for testing Inferno *3. EHR Practitioner App*.
1. Follow all instructions on [this page](./set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.

### Simulate the EHR launch for Inferno

The gateway includes a browser-based EHR launch initiator at `<FunctionBaseUrl>/api/ehr-launch`. It signs in the practitioner, sends the launch context to the gateway, and then redirects to Inferno with the `iss` and `launch` parameters. `FunctionBaseUrl` is in your azd environment output (`azd env get-values`).

Before you start:
- Add `<FunctionBaseUrl>/api/ehr-launch` as a **Single-page application (SPA)** redirect URI on the Auth Context Frontend App. See [Auth Context Frontend App Registration](./auth-context-frontend-app-registration.md).
- In Inferno, set **FHIR Endpoint** to exactly `<FunctionBaseUrl>`, with no trailing `/`. Inferno matches the launch's `iss` against this value exactly, so a trailing `/` or the FHIR service URL causes `Unable to find test run with identifier ...`.

To run the launch:
1. In Inferno, start **3. EHR Practitioner App**. Wait until it shows a dialog saying it is waiting for an EHR launch.
1. While that dialog is still open, browse to `<FunctionBaseUrl>/api/ehr-launch` and enter:
   - **SMART app launch URL:** `https://inferno.healthit.gov/suites/custom/smart/launch`
   - **Patient ID:** for example `PatientA`
   - **Encounter ID** (optional): for example `EncounterA`
1. Select **Sign in & Launch** and sign in as the practitioner test user, the one whose `fhirUser` is mapped to a `Practitioner`.
1. The browser goes to Inferno, which continues the launch through the gateway's authorize and token endpoints.


## Backend Service Client Application

Microsoft Entra ID does not support RSA384 and/or ES384 which is required by the SMART on FHIR implementation guide. In order to provide this capability, custom code is required to validate the JWT assertion and return a bearer token generated for the client with the corresponding client secret in an Azure KeyVault.

1. Create a new application in Microsoft Entra ID. No platform or redirect URL is needed.
1. Grant this application `FHIR SMART User` and `FHIR Data Exporter` role in your FHIR Service.
1. In API Permissions for this new application, add the below:
    - Your FHIR Resource API (Application)
        - user.all.rs
1. Grant admin consent for your Application on the API Permission page-->
1. Generate a secret for this application. Save this and the client id.
1. In the resource group that matches your environment, open the KeyVault with the suffix `backkv`.
1. Add a new secret that corresponds to the Application you just generated. 
    - Name: Application ID/Client ID of the application
    - Secret: The secret you generated for the application
    - Tags: Make sure to add the tag `jwks_url` with the backend service JWKS URL. For Inferno testing, this is: https://inferno.healthit.gov/suites/custom/g10_certification/.well-known/jwks.json
1. Save the client id for later testing.

## Asymmetric Client Standalone Application

Microsoft Entra ID does not support RSA384 and/or ES384 which is required by the SMART on FHIR implementation guide for [Client Authentication: Asymmetric (public key)](https://hl7.org/fhir/smart-app-launch/client-confidential-asymmetric.html). In order to provide this capability, custom code is required to validate the JWT assertion and return a bearer token generated for the client with the corresponding client secret in an Azure KeyVault.

1. Create a new application in Microsoft Entra ID. Make sure to select `Web` as the platform and add the redirect URL for Inferno (`https://inferno.healthit.gov/suites/custom/smart/redirect`).
1. In API Permissions for this new application, add the below:
    - Your FHIR Resource Application (Delegated)
    
| Common Scopes        | SMART V2 Scope                         | SMART V1 Scopes                               | Microsoft Graph Scopes |
|---------------------|-------------------------------------|---------------------------------------------|------------------------|
| `fhirUser`          | `patient.AllergyIntolerance.rs`     | `patient.AllergyIntolerance.read`           | `openid`               |
| `launch.patient`    | `patient.CarePlan.rs`               | `patient.CarePlan.read`                     | `offline_access`       |
|                     | `patient.CareTeam.rs`               | `patient.CareTeam.read`                     |                        |
|                     | `patient.Condition.rs`              | `patient.Condition.read`                    |                        |
|                     | `patient.Device.rs`                 | `patient.Device.read`                       |                        |
|                     | `patient.DiagnosticReport.rs`       | `patient.DiagnosticReport.read`             |                        |
|                     | `patient.DocumentReference.rs`      | `patient.DocumentReference.read`            |                        |
|                     | `patient.Encounter.rs`              | `patient.Encounter.read`                    |                        |
|                     | `patient.Goal.rs`                   | `patient.Goal.read`                         |                        |
|                     | `patient.Immunization.rs`           | `patient.Immunization.read`                 |                        |
|                     | `patient.Location.rs`               | `patient.Location.read`                     |                        |
|                     | `patient.MedicationRequest.rs`      | `patient.MedicationRequest.read`            |                        |
|                     | `patient.Medication.rs`             | `patient.Medication.read`                   |                        |
|                     | `patient.Observation.rs`            | `patient.Observation.read`                  |                        |
|                     | `patient.Organization.rs`           | `patient.Organization.read`                 |                        |
|                     | `patient.Patient.rs`                | `patient.Patient.read`                      |                        |
|                     | `patient.Practitioner.rs`           | `patient.Practitioner.read`                 |                        |
|                     | `patient.PractitionerRole.rs`       | `patient.PractitionerRole.read`             |                        |
|                     | `patient.Procedure.rs`              | `patient.Procedure.read`                    |                        |
|                     | `patient.Provenance.rs`             | `patient.Provenance.read`                   |                        |

1. Generate a secret for this application. Save this and the client id.
1. In the resource group that matches your environment, open the KeyVault with the suffix `backkv`.
1. Add a new secret that corresponds to the Application you just generated. (Screenshots from the Backend Service Client Application section can be used as a reference for performing below steps.)
    - Name: Application ID/Client ID of the application
    - Secret: The secret you generated for the application
    - Tags: Make sure to add the tag `jwks_url` with the backend service JWKS URL. For Inferno testing, this is: https://inferno.healthit.gov/suites/custom/g10_certification/.well-known/jwks.json
1. Save the client id for later testing.
1. Follow all instructions on [this page](./set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token.

## SMART Launch with Fine-Grained Scopes
The SMART Launch with Fine-Grained Scopes application is a standard confidential client application which represents an application that can protect a secret (section 9 of the test). Follow the below mentioned instructions. 

1. Create a new application in Microsoft Entra ID. Make sure to select `Web` as the platform and add the redirect URL for Inferno (`https://inferno.healthit.gov/suites/custom/smart/redirect`).
1. Entra ID does not support the use of `/` in granular scopes. Therefore, replace `/` with `%2f` when adding granular scopes in API Permissions for this new application as shown below:
    - Your FHIR Resource API (Delegated)
        - fhirUser
        - launch.patient
        - patient.condition.rs
        - patient.Condition.rs?category=http:%2f%2fhl7.org%2ffhir%2fus%2fcore%2fCodeSystem%2fcondition-category|health-concern
        - patient.Condition.rs?category=http:%2f%2fterminology.hl7.org%2fCodeSystem%2fcondition-category|encounter-diagnosis
        - patient.Observation.rs
        - patient.Observation.rs?category=http:%2f%2fhl7.org%2ffhir%2fus%2fcore%2fCodeSystem%2fus-core-category|sdoh
        - patient.Observation.rs?category=http:%2f%2fterminology.hl7.org%2fCodeSystem%2fobservation-category|laboratory
    - Microsoft Graph (Delegated)
        - openid
        - offline_access
1. Generate a secret for this application. Save this and the client id for testing Inferno *9.23. SMART App Launch with fine-grained scopes*.
1. Follow all instructions on [this page](./set-fhir-user-mapping.md) to enable mapping the `fhirUser` to the identity token. 


**[Back to Previous Page](../deployment-entra.md#end-to-end-test-with-the-smart-client-sample-app)**