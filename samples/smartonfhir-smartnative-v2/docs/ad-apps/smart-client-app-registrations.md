# SMART Client App Registrations (Microsoft Entra ID)

> [!TIP]
> If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](../troubleshooting.md).

This document walks through registering the **client applications** used to demo the four SMART on FHIR v2 launch flows against an `IdpType=EntraId` deployment of this sample:

1. **Standalone Patient Launch — Confidential client** (web app that can keep a secret).
2. **Standalone Patient Launch — Public client** (SPA / native app that cannot keep a secret).
3. **EHR Practitioner Launch — Confidential client** (launched from within an EHR session).
4. **Backend Service** (server-to-server, `client_credentials` with `private_key_jwt`).

Refresh-token flow is exercised by any of apps 1–3 when they include the `offline_access` scope — no separate app registration is required.

> [!NOTE]
> These steps assume you have already completed [FHIR Resource App Registration](./fhir-resource-app-registration.md) and [Auth Context Frontend App Registration](./auth-context-frontend-app-registration.md). The scopes referenced below are exposed by the **FHIR Resource App**.
>
> Throughout this document `<smart-client-callback-url>` is the redirect URI of the SMART client sample app you are using to drive the test flows. Replace it with the actual callback URL your client publishes (for example `https://<your-client-host>/callback`). Add `http://localhost/callback` alongside it for local testing if needed.

---

## Common prerequisites

For every app you register below:

- Sign in to the **Azure Portal** in the same tenant where you ran `azd up`.
- Open **Microsoft Entra ID → App registrations → New registration**.
- Set **Supported account types** to *Accounts in this organizational directory only*.

After creating each app, you will:

1. Add **API Permissions → Your FHIR Resource Application (Delegated)** for the SMART scopes shown below. In Entra ID, slashes (`/`) in SMART scope names are written as dots (`.`) — for example, `patient/Patient.rs` is registered as `patient.Patient.rs`.
2. Generate a **client secret** (skip for the SPA public client) and record both the **Application (client) ID** and the secret for use by the SMART client sample app.
3. Follow [Set fhirUser Claim Mapping](./set-fhir-user-mapping.md) to ensure `fhirUser` is emitted on the access token. This is configured on the FHIR Resource App, not the client app.

---

## 1. Standalone Patient Launch — Confidential client (Web)

This represents an application that can protect a client secret (server-rendered web apps, backend-of-frontend, etc.).

### Steps

1. **Create the app registration** with platform **Web** and add the redirect URI:

   ```
   <smart-client-callback-url>
   ```

2. Under **API Permissions → Add a permission → APIs my organization uses → \<your FHIR Resource Application\> (Delegated)**, add:

   | SMART context | Patient scopes (`*.rs`) | Patient scopes (`*.read`) | OIDC |
   | --- | --- | --- | --- |
   | `fhirUser` | `patient.AllergyIntolerance.rs` | `patient.AllergyIntolerance.read` | `openid` |
   | `launch.patient` | `patient.CarePlan.rs` | `patient.CarePlan.read` | `offline_access` |
   |  | `patient.CareTeam.rs` | `patient.CareTeam.read` |  |
   |  | `patient.Condition.rs` | `patient.Condition.read` |  |
   |  | `patient.Device.rs` | `patient.Device.read` |  |
   |  | `patient.DiagnosticReport.rs` | `patient.DiagnosticReport.read` |  |
   |  | `patient.DocumentReference.rs` | `patient.DocumentReference.read` |  |
   |  | `patient.Encounter.rs` | `patient.Encounter.read` |  |
   |  | `patient.Goal.rs` | `patient.Goal.read` |  |
   |  | `patient.Immunization.rs` | `patient.Immunization.read` |  |
   |  | `patient.Location.rs` | `patient.Location.read` |  |
   |  | `patient.Medication.rs` | `patient.Medication.read` |  |
   |  | `patient.MedicationRequest.rs` | `patient.MedicationRequest.read` |  |
   |  | `patient.Observation.rs` | `patient.Observation.read` |  |
   |  | `patient.Organization.rs` | `patient.Organization.read` |  |
   |  | `patient.Patient.rs` | `patient.Patient.read` |  |
   |  | `patient.Practitioner.rs` | `patient.Practitioner.read` |  |
   |  | `patient.PractitionerRole.rs` | `patient.PractitionerRole.read` |  |
   |  | `patient.Procedure.rs` | `patient.Procedure.read` |  |
   |  | `patient.Provenance.rs` | `patient.Provenance.read` |  |

   > Add `openid` and `offline_access` from **Microsoft Graph (Delegated)** if they are not already present. `offline_access` is required for the **Refresh** flow.

3. Under **Certificates & secrets → New client secret**, generate a secret. Record the **Client ID** and **secret value** — they go into the SMART client sample app configuration.

4. Follow [Set fhirUser Claim Mapping](./set-fhir-user-mapping.md) to map the `fhirUser` claim onto the access token.

---

## 2. Standalone Patient Launch — Public client (SPA)

This represents an application that cannot protect a client secret (single-page apps, mobile/native apps). It uses **PKCE** (`code_challenge` + `code_verifier`) instead of a client secret.

### Steps

1. **Create the app registration** with platform **Single-page application** and add the redirect URI:

   ```
   <smart-client-callback-url>
   ```

2. Add the same set of API Permissions as section 1 (the patient-compartment scopes plus `openid` and `offline_access`).

3. Do **not** generate a client secret. Public clients authenticate with PKCE only.

4. Follow [Set fhirUser Claim Mapping](./set-fhir-user-mapping.md).

5. Record the **Client ID** for use by the SMART client sample app.

---

## 3. EHR Practitioner Launch — Confidential client (Web)

This represents an application that is launched from within an EHR session with pre-existing context (patient already selected, encounter possibly selected).

### Steps

1. **Create the app registration** with platform **Web** and add the redirect URI:

   ```
   <smart-client-callback-url>
   ```

2. Under **API Permissions → Add a permission → APIs my organization uses → \<your FHIR Resource Application\> (Delegated)**, add:

   | SMART context | User scopes (`*.rs`) | User scopes (`*.read`) | OIDC |
   | --- | --- | --- | --- |
   | `fhirUser` | `user.AllergyIntolerance.rs` | `user.AllergyIntolerance.read` | `openid` |
   | `launch` | `user.CarePlan.rs` | `user.CarePlan.read` | `offline_access` |
   |  | `user.CareTeam.rs` | `user.CareTeam.read` |  |
   |  | `user.Condition.rs` | `user.Condition.read` |  |
   |  | `user.Device.rs` | `user.Device.read` |  |
   |  | `user.DiagnosticReport.rs` | `user.DiagnosticReport.read` |  |
   |  | `user.DocumentReference.rs` | `user.DocumentReference.read` |  |
   |  | `user.Encounter.rs` | `user.Encounter.read` |  |
   |  | `user.Goal.rs` | `user.Goal.read` |  |
   |  | `user.Immunization.rs` | `user.Immunization.read` |  |
   |  | `user.Location.rs` | `user.Location.read` |  |
   |  | `user.Medication.rs` | `user.Medication.read` |  |
   |  | `user.MedicationRequest.rs` | `user.MedicationRequest.read` |  |
   |  | `user.Observation.rs` | `user.Observation.read` |  |
   |  | `user.Organization.rs` | `user.Organization.read` |  |
   |  | `user.Patient.rs` | `user.Patient.read` |  |
   |  | `user.Practitioner.rs` | `user.Practitioner.read` |  |
   |  | `user.PractitionerRole.rs` | `user.PractitionerRole.read` |  |
   |  | `user.Procedure.rs` | `user.Procedure.read` |  |
   |  | `user.Provenance.rs` | `user.Provenance.read` |  |

   > Add `openid` and `offline_access` from **Microsoft Graph (Delegated)** if they are not already present.

3. Under **Certificates & secrets → New client secret**, generate a secret. Record the **Client ID** and **secret value**.

4. Follow [Set fhirUser Claim Mapping](./set-fhir-user-mapping.md).

5. The EHR launch flow requires an **EHR launch initiator** to first POST a launch payload to `<FunctionBaseUrl>/api/context-cache`. The SMART client sample app documents how to simulate this; follow its instructions for driving the EHR-launch demo.

---

## 4. Backend Service Client

Microsoft Entra ID does not natively support `private_key_jwt` with ES384 / RS384 (required by SMART Backend Services). The gateway in this sample bridges that gap: it validates the client's signed `client_assertion` against the client's published JWKS, then exchanges it for an Entra access token using a **Key Vault-stored client secret**.

### Steps

1. **Create the app registration** with **no platform** and **no redirect URI** (this is a service-to-service client).

2. Under **API permissions → Add a permission → APIs my organization uses → \<your FHIR Resource Application\> (Application)**, add the system scopes the backend service needs (for example `user.all.rs` or specific `system.<Resource>.rs` scopes if exposed). Then click **Grant admin consent**.

3. Under **Certificates & secrets → New client secret**, generate a secret. Record the **Client ID** and **secret value**.

4. Grant the application the FHIR roles it needs on the FHIR Service (for example, **FHIR Data Reader** or **FHIR SMART User**) via **FHIR Service → Access control (IAM) → Add role assignment**.

5. Have the backend service **publish its JWKS** at a stable HTTPS URL. This is the asymmetric public key it will sign `client_assertion`s with (ES384 or RS384).

6. **Add the secret to the backend services Key Vault** created by `azd up` (its name is written to `.azure/<env-name>/.env` as `BackendServiceKeyVaultName`):

   - **Name** = the **Client ID** of the Entra app registration.
   - **Value** = the **client secret value** from step 3.
   - **Tag** `jwks_url` = the JWKS URL from step 5.

   The Function App's managed identity is granted **Key Vault Secrets User** automatically. The deployer is granted **Key Vault Secrets Officer** so you can add/rotate secrets without an extra role assignment.

7. The backend service requests tokens via:

   ```http
   POST <FunctionBaseUrl>/api/token
   grant_type=client_credentials
   client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer
   client_assertion=<signed JWT>
   scope=system/<Resource>.rs ...
   ```

   The gateway validates the assertion (signature, `iss=sub=client_id`, `aud=token URL`, `jti` replay, lifetime ≤ 5 min), then calls Entra `/token` with `client_credentials` + the Key Vault secret.

---

## Outcome

When you finish you will have up to four client app registrations recorded with their **Client ID** (and **secret value** for confidential / backend apps), ready to plug into the SMART client sample app to drive:

- Standalone Patient Launch (confidential or public)
- EHR Practitioner Launch
- Backend Services
- Refresh-token flow (via `offline_access` on apps 1–3)

[Back to Microsoft Entra ID Deployment](../deployment-entra.md)
