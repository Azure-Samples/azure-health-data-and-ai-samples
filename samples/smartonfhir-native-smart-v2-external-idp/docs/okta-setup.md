> [!TIP]
> *If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](./troubleshooting.md).*

# Okta Setup Guide for SMART on FHIR v2

This guide walks you through configuring Okta as the external Identity Provider for the SMART on FHIR v2 sample. Complete **all sections** before running the Azure deployment (`azd up`).

---

## Table of Contents

1. [Create an Okta Authorization Server](#1-create-an-okta-authorization-server)
2. [Add SMART on FHIR Scopes](#2-add-smart-on-fhir-scopes)
3. [Create Test Users](#3-create-test-users)
4. [Add Custom Claims (fhirUser)](#4-add-custom-claims-fhiruser)
5. [Map FHIR Users to Test Accounts](#5-map-fhir-users-to-test-accounts)
6. [Create Client Application — Standalone Patient Launch](#6-create-client-application--standalone-patient-launch)
7. [Create Client Application — EHR Practitioner Launch](#7-create-client-application--ehr-practitioner-launch)
8. [Create Client Application — Backend Service (Client Credentials)](#8-create-client-application--backend-service-client-credentials)
9. [Configure Access Policies](#9-configure-access-policies)
10. [Verify Configuration](#10-verify-configuration)
11. [Collect Values for Azure Deployment](#11-collect-values-for-azure-deployment)

---

## 1. Create an Okta Authorization Server

You can use the **default** authorization server or create a custom one. A custom server is recommended for production scenarios.

### Using the Default Authorization Server

1. Sign in to the [Okta Admin Console](https://login.okta.com/).
2. Navigate to **Security** → **API**.
3. The **default** authorization server is listed. Note the **Issuer URI**:
   ```
   https://dev-12345678.okta.com/oauth2/default
   ```
   This is your **AuthorityURL** for the Azure deployment.

### Creating a Custom Authorization Server

1. Navigate to **Security** → **API** → **Authorization Servers**.
2. Click **Add Authorization Server**.
3. Fill in the details:

   | Field | Value | Example |
   |---|---|---|
   | **Name** | Descriptive name | `SMART on FHIR v2` |
   | **Audience** | Your FHIR service URL (set after Azure deployment) or a placeholder | `https://myhealth-fhirdata.fhir.azurehealthcareapis.com` |
   | **Description** | Optional description | `Authorization server for SMART on FHIR v2 sample` |

4. Click **Save**.
5. Note the **Issuer URI** — this is your **AuthorityURL**:
   ```
   https://dev-12345678.okta.com/oauth2/<server-id>
   ```

> [!NOTE]
> If you haven't deployed Azure resources yet, you can use a placeholder for the audience and update it after deployment. The FHIR URL follows the pattern: `https://{env-name}health-fhirdata.fhir.azurehealthcareapis.com`.

---

## 2. Add SMART on FHIR Scopes

SMART on FHIR requires specific scopes for patient-centric and practitioner-centric access. Add all scopes to your authorization server.

### Navigate to Scopes

1. Go to **Security** → **API** → **Authorization Servers**.
2. Select your authorization server (e.g., **default** or the custom one).
3. Click the **Scopes** tab.
4. Click **Add Scope** for each scope below.

### Patient-Centric Scopes (Standalone Launch)

Add each scope with **User consent** set to `Required` so users are prompted to approve the scopes during authorization:

| Scope Name | Display Name | Description |
|---|---|---|
| `launch/patient` | Launch Patient | Launch scope for standalone patient access |
| `fhirUser` | FHIR User | Access to user's FHIR resource |
| `patient/AllergyIntolerance.rs` | Patient AllergyIntolerance | Read/search AllergyIntolerance resources |
| `patient/CarePlan.rs` | Patient CarePlan | Read/search CarePlan resources |
| `patient/CareTeam.rs` | Patient CareTeam | Read/search CareTeam resources |
| `patient/Condition.rs` | Patient Condition | Read/search Condition resources |
| `patient/Device.rs` | Patient Device | Read/search Device resources |
| `patient/DiagnosticReport.rs` | Patient DiagnosticReport | Read/search DiagnosticReport resources |
| `patient/DocumentReference.rs` | Patient DocumentReference | Read/search DocumentReference resources |
| `patient/Encounter.rs` | Patient Encounter | Read/search Encounter resources |
| `patient/Goal.rs` | Patient Goal | Read/search Goal resources |
| `patient/Immunization.rs` | Patient Immunization | Read/search Immunization resources |
| `patient/Location.rs` | Patient Location | Read/search Location resources |
| `patient/Medication.rs` | Patient Medication | Read/search Medication resources |
| `patient/MedicationRequest.rs` | Patient MedicationRequest | Read/search MedicationRequest resources |
| `patient/Observation.rs` | Patient Observation | Read/search Observation resources |
| `patient/Organization.rs` | Patient Organization | Read/search Organization resources |
| `patient/Patient.rs` | Patient Patient | Read/search Patient resources |
| `patient/Practitioner.rs` | Patient Practitioner | Read/search Practitioner resources |
| `patient/PractitionerRole.rs` | Patient PractitionerRole | Read/search PractitionerRole resources |
| `patient/Procedure.rs` | Patient Procedure | Read/search Procedure resources |
| `patient/Provenance.rs` | Patient Provenance | Read/search Provenance resources |

### Practitioner-Centric Scopes (EHR Launch)

| Scope Name | Display Name | Description |
|---|---|---|
| `launch` | EHR Launch | EHR launch context scope |
| `user/AllergyIntolerance.rs` | User AllergyIntolerance | Read/search AllergyIntolerance resources |
| `user/CarePlan.rs` | User CarePlan | Read/search CarePlan resources |
| `user/CareTeam.rs` | User CareTeam | Read/search CareTeam resources |
| `user/Condition.rs` | User Condition | Read/search Condition resources |
| `user/Device.rs` | User Device | Read/search Device resources |
| `user/DiagnosticReport.rs` | User DiagnosticReport | Read/search DiagnosticReport resources |
| `user/DocumentReference.rs` | User DocumentReference | Read/search DocumentReference resources |
| `user/Encounter.rs` | User Encounter | Read/search Encounter resources |
| `user/Goal.rs` | User Goal | Read/search Goal resources |
| `user/Immunization.rs` | User Immunization | Read/search Immunization resources |
| `user/Location.rs` | User Location | Read/search Location resources |
| `user/Medication.rs` | User Medication | Read/search Medication resources |
| `user/MedicationRequest.rs` | User MedicationRequest | Read/search MedicationRequest resources |
| `user/Observation.rs` | User Observation | Read/search Observation resources |
| `user/Organization.rs` | User Organization | Read/search Organization resources |
| `user/Patient.rs` | User Patient | Read/search Patient resources |
| `user/Practitioner.rs` | User Practitioner | Read/search Practitioner resources |
| `user/PractitionerRole.rs` | User PractitionerRole | Read/search PractitionerRole resources |
| `user/Procedure.rs` | User Procedure | Read/search Procedure resources |
| `user/Provenance.rs` | User Provenance | Read/search Provenance resources |

### Backend Service Scopes (Client Credentials)

| Scope Name | Display Name | Description |
|---|---|---|
| `system/*.read` | System Read All | Read all resources (backend service) |

### Standard OIDC Scopes

These are typically included by default in Okta, but verify they exist:

| Scope Name | Purpose |
|---|---|
| `openid` | OpenID Connect authentication |
| `offline_access` | Refresh token support |

---

## 3. Create Test Users

Create two test users in Okta — one for the Patient persona and one for the Practitioner persona.

### Create a Patient Test User

1. Navigate to **Directory** → **People**.
2. Click **Add Person**.
3. Fill in the details:

   | Field | Example Value |
   |---|---|
   | **First Name** | `Test` |
   | **Last Name** | `Patient` |
   | **Username (Email)** | `testpatient@yourdomain.com` |
   | **Primary Email** | `testpatient@yourdomain.com` |
   | **Password** | Set by admin / set by user on first login |

4. Click **Save**.
5. **Record the user ID** — Click on the user → the Okta **User ID** is visible in the browser URL bar (e.g., `https://dev-12345678-admin.okta.com/admin/user/profile/view/00u1abc2defGHIjkl5d7`).

### Create a Practitioner Test User

1. Click **Add Person** again.
2. Fill in the details:

   | Field | Example Value |
   |---|---|
   | **First Name** | `Test` |
   | **Last Name** | `Practitioner` |
   | **Username (Email)** | `testpractitioner@yourdomain.com` |
   | **Primary Email** | `testpractitioner@yourdomain.com` |
   | **Password** | Set by admin / set by user on first login |

3. Click **Save**.
4. **Record the user ID** from the browser URL bar.

---

## 4. Add Custom Claims (fhirUser)

The `fhirUser` claim in the access token tells the FHIR service which FHIR resource the authenticated user maps to. You need to add this as a custom claim on your authorization server.

### Step 1: Add a Custom User Profile Attribute

1. Navigate to **Directory** → **Profile Editor**.
2. Click **Okta User (default)** (the user profile, not an app profile).
3. Click **Add Attribute**.
4. Fill in:

   | Field | Value |
   |---|---|
   | **Data Type** | `string` |
   | **Display Name** | `FHIR User` |
   | **Variable Name** | `fhirUser` |
   | **Description** | `FHIR resource URL for SMART on FHIR` |
   | **Attribute Required** | No |

5. Click **Save**.

### Step 2: Add fhirUser as a Claim on the Authorization Server

1. Navigate to **Security** → **API** → **Authorization Servers**.
2. Select your authorization server.
3. Click the **Claims** tab.
4. Click **Add Claim**.
5. Fill in:

   | Field | Value |
   |---|---|
   | **Name** | `fhirUser` |
   | **Include in token type** | **Access Token** (select **Always**) |
   | **Value type** | `Expression` |
   | **Value** | `user.fhirUser` |
   | **Include in** | `Any scope` (or restrict to `fhirUser` scope) |

6. Click **Create**.

### Step 3: Add appid as a Claim on the Authorization Server

The FHIR service uses the `appid` claim to identify the calling application. In Okta, this must be added as a custom claim mapped to the client ID.

1. Still on the **Claims** tab, click **Add Claim**.
2. Fill in:

   | Field | Value |
   |---|---|
   | **Name** | `appid` |
   | **Include in token type** | **Access Token** (select **Always**) |
   | **Value type** | `Expression` |
   | **Value** | `app.clientId` |
   | **Include in** | `Any scope` |

3. Click **Create**.

> [!IMPORTANT]
> Both the `fhirUser` and `appid` claims must be included in the **Access Token**, not just the ID Token. The FHIR service reads `appid` to identify which application is making the request, and the Function App reads `fhirUser` to augment the token response.

---

## 5. Map FHIR Users to Test Accounts

Now populate the `fhirUser` attribute for each test user with the corresponding FHIR resource URL.

> [!NOTE]
> The FHIR URL follows the pattern `https://{env-name}health-fhirdata.fhir.azurehealthcareapis.com`. If you haven't deployed yet, use a placeholder and update after deployment.

### Map the Patient Test User

1. Navigate to **Directory** → **People**.
2. Click on the **Patient test user** (e.g., `testpatient@yourdomain.com`).
3. Click the **Profile** tab → **Edit**.
4. Find the **FHIR User** field and set it to:
   ```
   https://<env-name>health-fhirdata.fhir.azurehealthcareapis.com/Patient/PatientA
   ```
5. Click **Save**.

**Example:**
```
https://smartfhirokta01health-fhirdata.fhir.azurehealthcareapis.com/Patient/PatientA
```

### Map the Practitioner Test User

1. Click on the **Practitioner test user** (e.g., `testpractitioner@yourdomain.com`).
2. Click the **Profile** tab → **Edit**.
3. Set the **FHIR User** field to:
   ```
   https://<env-name>health-fhirdata.fhir.azurehealthcareapis.com/Practitioner/PractitionerC1
   ```
4. Click **Save**.

**Example:**
```
https://smartfhirokta01health-fhirdata.fhir.azurehealthcareapis.com/Practitioner/PractitionerC1
```

---

## 6. Create Client Application — Standalone Patient Launch

This application is used by SMART apps launching outside an EHR session (patient-facing apps).

### Create the Application

1. Navigate to **Applications** → **Applications**.
2. Click **Create App Integration**.
3. Select:
   - **Sign-in method**: `OIDC - OpenID Connect`
   - **Application type**: `Web Application`
4. Click **Next**.
5. Fill in:

   | Field | Value |
   |---|---|
   | **App integration name** | `SMART Standalone Patient App` |
   | **Grant type** | Check: `Authorization Code`, `Refresh Token` |
   | **Sign-in redirect URIs** | `<your-smart-client-callback-url>`  |
   | | `http://localhost/callback` (for local development/testing) |
   | **Sign-out redirect URIs** | `http://localhost` |
   | **Controlled access** | `Allow everyone in your organization to access` (or limit to specific groups) |

6. Click **Save**.

### Record Application Credentials

After saving, note the following from the **General** tab:

| Value | Where to Find | Example |
|---|---|---|
| **Client ID** | General → Client Credentials | `0oa1abc2defGHIjkl5d7` |
| **Client Secret** | General → Client Credentials → Copy | `abcdef123456...` |

> [!NOTE]
> If you want a **public client** (no secret, PKCE-only), set the **Client authentication** to `None` under **General Settings** → **Client Credentials**. Public clients must use PKCE (`code_challenge` + `code_verifier`).

### Scopes Used by This Application

Custom SMART scopes are **not** configured on the application's "Okta API Scopes" tab (that tab is only for Okta's own management API scopes). The SMART scopes you added to the authorization server in [Section 2](#2-add-smart-on-fhir-scopes) are available to any client app — the authorization server's **Access Policies** control which grant types and scopes are permitted.

This application will request the following scopes during authorization:

```
openid offline_access fhirUser launch/patient
patient/AllergyIntolerance.rs patient/CarePlan.rs patient/CareTeam.rs
patient/Condition.rs patient/Device.rs patient/DiagnosticReport.rs
patient/DocumentReference.rs patient/Encounter.rs patient/Goal.rs
patient/Immunization.rs patient/Location.rs patient/Medication.rs
patient/MedicationRequest.rs patient/Observation.rs patient/Organization.rs
patient/Patient.rs patient/Practitioner.rs patient/PractitionerRole.rs
patient/Procedure.rs patient/Provenance.rs
```

---

## 7. Create Client Application — EHR Practitioner Launch

This application is used when the app is launched from within an EHR session with pre-existing context (e.g., patient already selected).

### Create the Application

1. Navigate to **Applications** → **Applications**.
2. Click **Create App Integration**.
3. Select:
   - **Sign-in method**: `OIDC - OpenID Connect`
   - **Application type**: `Web Application`
4. Click **Next**.
5. Fill in:

   | Field | Value |
   |---|---|
   | **App integration name** | `SMART EHR Practitioner App` |
   | **Grant type** | Check: `Authorization Code`, `Refresh Token` |
   | **Sign-in redirect URIs** | `<your-smart-client-callback-url>`|
   | | `http://localhost/callback` (for local development/testing) |
   | **Sign-out redirect URIs** | `http://localhost` |
   | **Controlled access** | `Allow everyone in your organization to access` |

6. Click **Save**.

### Record Application Credentials

Note the **Client ID** and **Client Secret** from the **General** tab.

### Scopes Used by This Application

As with the Standalone app, custom SMART scopes are defined on the authorization server ([Section 2](#2-add-smart-on-fhir-scopes)) and do not appear on the application's "Okta API Scopes" tab.

This application will request the following scopes during authorization:

```
openid offline_access fhirUser launch
user/AllergyIntolerance.rs user/CarePlan.rs user/CareTeam.rs
user/Condition.rs user/Device.rs user/DiagnosticReport.rs
user/DocumentReference.rs user/Encounter.rs user/Goal.rs
user/Immunization.rs user/Location.rs user/Medication.rs
user/MedicationRequest.rs user/Observation.rs user/Organization.rs
user/Patient.rs user/Practitioner.rs user/PractitionerRole.rs
user/Procedure.rs user/Provenance.rs
```

---

## 8. Create Client Application — Backend Service (Client Credentials)

This application is used for server-to-server access with no user interaction (e.g., bulk data, system-level reads).

### Create the Application

1. Navigate to **Applications** → **Applications**.
2. Click **Create App Integration**.
3. Select:
   - **Sign-in method**: `OIDC - OpenID Connect`
   - **Application type**: `Service (Machine-to-Machine)`
4. Click **Next**.
5. Fill in:

   | Field | Value |
   |---|---|
   | **App integration name** | `SMART Backend Service` |
   | **Controlled access** | `Allow everyone in your organization to access` |

6. Click **Save**.

### Configure Client Authentication (JWT Bearer)

Backend services use a signed JWT assertion instead of a client secret.

1. Go to your application → **General** tab.
2. Under **Client Credentials**, select **Public key / Private key**.
3. Click **Add Key** → either:
   - **Generate a new key** — download and save the private key securely.
   - **Paste a public key** — if you already have a key pair.
4. Click **Done**.

### Scopes and Access Policy for Backend Service

The `system/*.read` scope is a custom scope on the authorization server. You must create an **Access Policy rule** that grants this scope to the backend service app via the Client Credentials grant.

1. Go to **Security** → **API** → **Authorization Servers** → your server.
2. Click **Access Policies** tab.
3. Either edit the existing **Default Policy** or create a new one.
4. Add/edit a **Rule**:

   | Field | Value |
   |---|---|
   | **Grant type** | Check: `Client Credentials` |
   | **Scopes** | `system/*.read` |
   | **Assigned to** | The backend service application |

5. Click **Save**.

---

## 9. Configure Access Policies

Since all applications were created with **Controlled access** set to `Allow everyone in your organization to access`, all users in your Okta org are implicitly assigned to these apps. No manual user assignment is needed.

### Verify Access Policies

Ensure your authorization server has access policies that allow the configured grant types:

1. Go to **Security** → **API** → **Authorization Servers** → your server.
2. Click **Access Policies** tab.
3. Verify or create a policy with rules for:

   | Rule | Grant Types | Scopes |
   |---|---|---|
   | **Standalone Launch** | Authorization Code, Refresh Token | `openid`, `offline_access`, `fhirUser`, `launch/patient`, `patient/*.rs` |
   | **EHR Launch** | Authorization Code, Refresh Token | `openid`, `offline_access`, `fhirUser`, `launch`, `user/*.rs` |
   | **Backend Service** | Client Credentials | `system/*.read` |

---

## 10. Verify Configuration

### Test Token Generation

Use Okta's built-in **Token Preview** to verify your setup:

1. Go to **Security** → **API** → **Authorization Servers** → your server.
2. Click **Token Preview** tab.
3. Test for the **Standalone Patient App**:

   | Field | Value |
   |---|---|
   | **Grant Type** | Authorization Code |
   | **User** | Patient test user |
   | **Scopes** | `openid fhirUser launch/patient patient/Patient.rs patient/Observation.rs` |
   | **Client** | SMART Standalone Patient App |

4. Click **Preview Token**.
5. Verify in the **Access Token**:
   - `sub` claim is present
   - `fhirUser` claim contains the FHIR Patient URL
   - `scp` (scope) array includes the requested SMART scopes

### Test OIDC Discovery

Verify the OIDC discovery endpoint is accessible:

```bash
curl https://dev-12345678.okta.com/oauth2/default/.well-known/openid-configuration
```

Expected response should include:
- `issuer` — matches your AuthorityURL
- `authorization_endpoint` — the `/authorize` URL
- `token_endpoint` — the `/token` URL
- `scopes_supported` — should list your custom SMART scopes

---

## 11. Collect Values for Azure Deployment

After completing all Okta configuration, gather the following values for the Azure deployment:

| Value | Where to Find | Used In |
|---|---|---|
| **AuthorityURL** | Security → API → Authorization Servers → Issuer URI | `azd env set AuthorityURL` |
| **Standalone Client ID** | Applications → SMART Standalone Patient App → General → Client ID | Postman / REST client testing |
| **Standalone Client Secret** | Applications → SMART Standalone Patient App → General → Client Secret | Postman / REST client testing |
| **EHR Client ID** | Applications → SMART EHR Practitioner App → General → Client ID | Postman / REST client testing |
| **EHR Client Secret** | Applications → SMART EHR Practitioner App → General → Client Secret | Postman / REST client testing |
| **Backend Service Client ID** | Applications → SMART Backend Service → General → Client ID | Backend service testing |
| **Patient Test User Email** | Directory → People → Patient user | Authentication testing |
| **Practitioner Test User Email** | Directory → People → Practitioner user | Authentication testing |

### Set Azure Deployment Environment Values

```powershell
# Required
azd env set AuthorityURL "https://dev-12345678.okta.com/oauth2/default"

# Optional (defaults to sub, which is correct for Okta)
azd env set UserIdClaimType "sub"
```

## Next Steps

Once Okta configuration is complete, proceed to the [Deployment Guide](./deployment.md) to deploy the Azure infrastructure and Function App.

---

**[Back to Previous Page](../README.md)**
