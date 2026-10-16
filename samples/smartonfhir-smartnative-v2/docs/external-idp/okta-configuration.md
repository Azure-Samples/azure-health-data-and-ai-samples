# Configure Okta as the SMART Authorization Server

This document covers everything you need to do **inside Okta** so that the SMART on FHIR v2 native IdP-agnostic sample can use it as the upstream authorization server. 

The same general steps apply to any SMART-capable OpenID Provider (Auth0, Ping, etc.) that can issue SMART scopes and a `fhirUser` claim natively. Where a step is Okta-specific, it is called out.

## What you will produce

By the end of this guide you will have:

- An **Authorization Server** in Okta with the SMART scopes enabled.
- The **authority URL** (issuer) you will hand to `azd` as `AuthorityURL`.
- The **audience** value (`aud` claim) you will hand to `azd` as `FhirAudience` (or that defaults to the FHIR Service URL).
- Claim mappings on the access token for `fhirUser`, `appid`, `patient`, `encounter`, `launch`, and `scope`.
- A **custom user profile attribute** for `fhirUser` mapped onto each test user.
- An **access policy** that lets the configured grant types issue the SMART scopes.
- *(Optional)* a smoke-test of OIDC discovery before deployment.

> [!NOTE]
> All admin operations below happen in the Okta Admin Console. You need an Okta admin role with permissions to manage Authorization Servers, Applications, and User Profiles.
>
> Client application registrations (Standalone Patient, EHR Practitioner, Backend Service) are documented in the **SMART client sample app** repository — they are not duplicated here. This document covers the upstream authorization server only.

---

## A. Choose an Authorization Server

Okta exposes two kinds of authorization servers:

| Type | Authority URL | When to use |
| --- | --- | --- |
| Org default | `https://<okta-domain>/oauth2/default` | Quick setup or single-tenant lab. |
| Custom Authorization Server | `https://<okta-domain>/oauth2/<auth-server-id>` | Recommended for production — lets you scope claim and scope policies to this sample. |

To create a custom server: **Security → API → Authorization Servers → Add Authorization Server**. Fill in a descriptive name, a placeholder audience (you will set the real one in step B), and save.

Note the **Issuer URI** — this is the value you will set as `AuthorityURL` on the `azd` environment.

---

## B. Set the audience

Open **Security → API → Authorization Servers → \<your server\> → Settings** and set the **Audience** to the value you will use as `FhirAudience`. Typical choices:

- The FHIR Service URL itself (e.g. `https://<env-name>health-fhirdata.fhir.azurehealthcareapis.com`).
- A separate logical audience URI (e.g. `api://smart-on-fhir`) — in which case set `FhirAudience` to the same value at `azd up` time.

> [!TIP]
> If you have not yet run `azd up`, use a placeholder audience now and update it after the FHIR Service URL is known.

---

## C. Define the SMART scopes

In **Security → API → Authorization Servers → \<your server\> → Scopes**, add every SMART scope you intend to issue. Set **User consent** to `Required` on the user-facing scopes so users are prompted to approve them during authorization.

### Standard OIDC

| Scope | Description |
| --- | --- |
| `openid` | OpenID Connect authentication. |
| `offline_access` | Refresh-token support. Required for the SMART **Refresh** flow. |
| `fhirUser` | Issues the `fhirUser` claim on the access token. |

### Standalone patient launch (US Core read/search)

| Scope | Description |
| --- | --- |
| `launch/patient` | Patient launch context. |
| `patient/AllergyIntolerance.rs` | Read/search AllergyIntolerance. |
| `patient/CarePlan.rs` | Read/search CarePlan. |
| `patient/CareTeam.rs` | Read/search CareTeam. |
| `patient/Condition.rs` | Read/search Condition. |
| `patient/Device.rs` | Read/search Device. |
| `patient/DiagnosticReport.rs` | Read/search DiagnosticReport. |
| `patient/DocumentReference.rs` | Read/search DocumentReference. |
| `patient/Encounter.rs` | Read/search Encounter. |
| `patient/Goal.rs` | Read/search Goal. |
| `patient/Immunization.rs` | Read/search Immunization. |
| `patient/Location.rs` | Read/search Location. |
| `patient/Medication.rs` | Read/search Medication. |
| `patient/MedicationRequest.rs` | Read/search MedicationRequest. |
| `patient/Observation.rs` | Read/search Observation. |
| `patient/Organization.rs` | Read/search Organization. |
| `patient/Patient.rs` | Read/search Patient. |
| `patient/Practitioner.rs` | Read/search Practitioner. |
| `patient/PractitionerRole.rs` | Read/search PractitionerRole. |
| `patient/Procedure.rs` | Read/search Procedure. |
| `patient/Provenance.rs` | Read/search Provenance. |

### EHR practitioner launch (US Core read/search)

| Scope | Description |
| --- | --- |
| `launch` | EHR launch context. |
| `user/AllergyIntolerance.rs` | Read/search AllergyIntolerance. |
| `user/CarePlan.rs` | Read/search CarePlan. |
| `user/CareTeam.rs` | Read/search CareTeam. |
| `user/Condition.rs` | Read/search Condition. |
| `user/Device.rs` | Read/search Device. |
| `user/DiagnosticReport.rs` | Read/search DiagnosticReport. |
| `user/DocumentReference.rs` | Read/search DocumentReference. |
| `user/Encounter.rs` | Read/search Encounter. |
| `user/Goal.rs` | Read/search Goal. |
| `user/Immunization.rs` | Read/search Immunization. |
| `user/Location.rs` | Read/search Location. |
| `user/Medication.rs` | Read/search Medication. |
| `user/MedicationRequest.rs` | Read/search MedicationRequest. |
| `user/Observation.rs` | Read/search Observation. |
| `user/Organization.rs` | Read/search Organization. |
| `user/Patient.rs` | Read/search Patient. |
| `user/Practitioner.rs` | Read/search Practitioner. |
| `user/PractitionerRole.rs` | Read/search PractitionerRole. |
| `user/Procedure.rs` | Read/search Procedure. |
| `user/Provenance.rs` | Read/search Provenance. |

### Backend services

| Scope | Description |
| --- | --- |
| `system/*.rs` | System-level read/search for backend services (`client_credentials`). |

---

## D. Configure claim mappings on the access token

In **Security → API → Authorization Servers → \<your server\> → Claims**, add the following claims to the **access token** (not the ID token). All five are required for SMART on FHIR v2 to work end to end.

| Claim | Include | Value type | Value expression | Why it is needed |
| --- | --- | --- | --- | --- |
| `fhirUser` | Always | Expression | `user.fhirUser` | Maps the custom user attribute defined in step E onto the token. The FHIR Service uses this for SMART compartment scope evaluation. |
| `appid` | Always | Expression | `app.clientId` | The **FHIR Service** uses `appid` to identify the calling client and match it against the `applications[]` list on the SMART identity provider entry (configured in [step 6 of the deployment guide](../deployment-okta.md#6-register-smart-client-ids-on-the-fhir-service)). **Without this claim, FHIR requests are rejected with HTTP 401 even when everything else is correct.** |
| `patient` | Always | Expression | `user.fhirPatient` (or context-supplied) | Standalone patient launch / EHR launch context. |
| `encounter` | Always | Expression | Context-supplied | EHR launch when an encounter is in context. |
| `launch` | Always | Expression | Context-supplied | Opaque launch token for EHR launch only; cached by the gateway. |

> Okta emits the `scope` claim by default. Verify it is present. Confirm the **`aud`** claim equals the **Audience** you set in step B.

---

## E. Add the `fhirUser` user profile attribute

To support the `user.fhirUser` expression in step D, each Okta user profile needs a custom attribute that holds the FHIR URL of the user resource.

1. Open **Directory → Profile Editor → User (default)**.
2. Click **Add Attribute** and fill in:
   - **Data type**: `string`
   - **Display name**: `FHIR User`
   - **Variable name**: `fhirUser`
   - **Description**: `FHIR resource URL for SMART on FHIR`
   - **Attribute length**: long enough for a full FHIR URL (e.g. 256 characters).
3. Save.

---

## F. Map the test users

Create two test users (Patient persona, Practitioner persona) under **Directory → People → Add Person**, then set their `fhirUser` attribute to the matching sample-data resource:

- Patient persona → `https://<fhir-url>/Patient/PatientA`
- Practitioner persona → `https://<fhir-url>/Practitioner/PractitionerC1`

Update each user via **Directory → People → \<user\> → Profile → Edit**, set the **FHIR User** field, and save.

> The `<fhir-url>` value here must match `FhirUrl` from the `azd` environment outputs after deployment. If you have not yet deployed, use the predicted FHIR URL (`https://<env-name>health-fhirdata.fhir.azurehealthcareapis.com`) and verify after `azd up`.

---

## G. Configure access policies

Okta will not issue tokens for the scopes above unless an **Access Policy** allows it.

1. Go to **Security → API → Authorization Servers → \<your server\> → Access Policies**.
2. Edit the existing **Default Policy** or create a new one targeting the sample's client applications.
3. Add or edit a **Rule** for each grant type you need:

   | Rule | Grant type | Scopes |
   | --- | --- | --- |
   | Standalone patient launch | Authorization Code, Refresh Token | `openid`, `offline_access`, `fhirUser`, `launch/patient`, `patient/*.rs` |
   | EHR practitioner launch | Authorization Code, Refresh Token | `openid`, `offline_access`, `fhirUser`, `launch`, `user/*.rs` |
   | Backend services | Client Credentials | `system/*.rs` |

4. Save.

> Without these rules, even a correctly configured client will receive `invalid_scope` from Okta.

---

## H. (Optional) Verify OIDC discovery

Before running `azd up`, smoke-test the discovery endpoint to confirm the authorization server is reachable and exposes the SMART scopes:

```http
GET <AuthorityURL>/.well-known/openid-configuration
```

Expected response should include:

- `issuer` — matches your `AuthorityURL`.
- `authorization_endpoint` and `token_endpoint`.
- `scopes_supported` — should list the custom SMART scopes added in step C.

---

## I. Hand-off values

Bring these values back to the deployment guide:

| Value | Notes |
| --- | --- |
| `AuthorityURL` | Issuer URL from step A. |
| `FhirAudience` *(optional)* | Audience from step B if different from the FHIR Service URL. |
| Test user account credentials | Patient persona and Practitioner persona, with `fhirUser` attribute set per step F. |
| Backend services client IDs *(optional)* | For the FHIR Service IdP whitelist. Documented in the SMART client sample app. |

> No `UserIdClaimType` is required — the gateway derives the user-id claim from `IdpType` automatically (Okta tokens are read via `sub` on the access token).

[Back to Okta Deployment Guide](../deployment-okta.md)
