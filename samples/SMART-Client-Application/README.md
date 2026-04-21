# SMART on FHIR v2 — Native Client Application

A full-featured ASP.NET Core 8 demonstration of all four **SMART on FHIR v2** authorization flows using **Okta** as the identity provider and **Azure Health Data Services** as the FHIR server.

| Flow | Grant Type | Client Type |
|------|-----------|-------------|
| Standalone (Public) | `authorization_code` + PKCE | Public |
| Standalone (Confidential) | `authorization_code` + PKCE + `client_secret` | Confidential |
| EHR Launch | `authorization_code` + PKCE + `launch` context | Confidential |
| Backend Services (M2M) | `client_credentials` + `private_key_jwt` (ES384) | Machine-to-Machine |

---

## Table of Contents

1. [Components](#components)
2. [Prerequisites](#prerequisites)
3. [Repository Structure](#repository-structure)
4. [Okta Configuration](#okta-configuration)
5. [Application Configuration](#application-configuration)
6. [Generate the ES384 Private Key](#generate-the-es384-private-key)
7. [Build and Run](#build-and-run)
8. [Using the Application](#using-the-application)
9. [Supported FHIR Resources](#supported-fhir-resources)
10. [Troubleshooting](#troubleshooting)

---

## Components

### SmartOnFhirDemo — ASP.NET Core 8 MVC Application

#### Controllers

| Controller | Purpose |
|------------|---------|
| `HomeController` | Renders the dashboard and manages session state |
| `SmartController` | Handles `/login`, `/callback`, `/fhir`, and other OAuth/FHIR endpoints |

#### Services

| Service | Purpose |
|---------|---------|
| `SmartConfigService` | Discovers FHIR server capabilities via `.well-known/smart-configuration` |
| `AuthService` | Manages OAuth 2.0 authorization code + PKCE flows (Standalone and EHR Launch) |
| `FhirService` | Makes authenticated FHIR API requests using bearer tokens |
| `BackendTokenService` | Orchestrates machine-to-machine token acquisition for the Backend Services flow |

### OktaSmartBackend.TokenClient — Class Library

| Class | Purpose |
|-------|---------|
| `OktaM2mClient` | Builds and sends the `client_credentials` token request to Okta |
| `OktaM2mJwtAssertion` | Creates and signs the ES384 JWT client assertion used for `private_key_jwt` authentication |

### External Dependencies

| System | Role |
|--------|------|
| **Okta** | Identity provider — handles OAuth 2.0 / OIDC authentication and authorization |
| **Azure Health Data Services** | FHIR R4 server — stores and serves clinical data over HTTPS |

---

## Prerequisites

| Requirement | Version |
|-------------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | **8.0** or later |
| [Okta Developer Account](https://developer.okta.com/signup/) | Free tier is sufficient |
| [OpenSSL](https://www.openssl.org/) *(or equivalent)* | Any recent version — for ES384 key generation |
| FHIR R4 Server | Azure Health Data Services or compatible endpoint |

> **Note:** The project is pre-configured to use a hosted FHIR simulation server and Okta tenant. To use your own, update the configuration values described below.

---


## Okta Configuration

You need **two** application registrations in your Okta tenant (or three if using the EHR simulator identity flow).

### 1. Standalone / EHR Launch Application

| Setting | Value |
|---------|-------|
| Application type | **Web** (Confidential) or **SPA** (Public) |
| Sign-in redirect URI | `https://localhost:53361/callback` |
| Grant types | Authorization Code |
| PKCE | Required |
| Scopes (Authorization Server) | `openid`, `fhirUser`, `offline_access`, `launch/patient`, `launch`, `patient/Patient.rs`, `patient/CarePlan.rs`, etc. |

> For **Standalone Public**, register as an SPA (no client secret).
> For **Standalone Confidential** or **EHR Launch**, register as a Web app and note the client secret.

### 2. Backend Services (M2M) Application

| Setting | Value |
|---------|-------|
| Application type | **API Services** (machine-to-machine) |
| Grant types | Client Credentials |
| Client authentication | `private_key_jwt` |
| Token endpoint auth method | Signed JWT (ES384) |
| Scopes (Authorization Server) | `system/*.rs` |

Upload the **public key** (see [Generate the ES384 Private Key](#generate-the-es384-private-key)) to this application's JWKS and note the **Key ID** assigned by Okta.

### 3. EHR Simulator Identity Application *(optional)*

| Setting | Value |
|---------|-------|
| Application type | **Web** |
| Sign-in redirect URI | `https://localhost:53361/usercontext/callback` |
| Grant types | Authorization Code |
| Scopes | `openid` only |

This lightweight registration is used solely to authenticate the simulated EHR user before caching launch context.

---

## Application Configuration

All settings live in **`SMART-Native-Standalone-EHR-Launch/appsettings.json`**. Open it and update the values for your environment:

```jsonc
{
  "SmartOnFhir": {
    // ── FHIR Server ──
    "FhirBaseUrl":    "<YOUR_FHIR_SERVER_BASE_URL>",
    "FhirAudience":   "<YOUR_FHIR_AUDIENCE_URL>",

    // ── Standalone / EHR OAuth Client ──
    "ClientId":       "<YOUR_OAUTH_CLIENT_ID>",
    "ClientSecret":   "<YOUR_OAUTH_CLIENT_SECRET>",    // leave empty for public clients
    "RedirectUri":    "https://localhost:53361/callback",

    // ── Context Cache (EHR Simulator) ──
    "ContextCacheUrl": "<YOUR_CONTEXT_CACHE_API_URL>",

    // ── EHR Simulator Identity ──
    "UserIdClaimType":         "uid",
    "UserContextClientId":     "<YOUR_EHR_IDENTITY_CLIENT_ID>",
    "UserContextClientSecret": "<YOUR_EHR_IDENTITY_CLIENT_SECRET>",
    "UserContextRedirectUri":  "https://localhost:53361/usercontext/callback"
  },

  "BackendServices": {
    "FhirBaseUrl":    "<YOUR_BACKEND_FHIR_SERVER_URL>",
    "Domain":         "<YOUR_OKTA_DOMAIN>",
    "AuthServerId":   "<YOUR_AUTH_SERVER_ID>",
    "ClientId":       "<YOUR_M2M_CLIENT_ID>",
    "Scope":          "system/*.rs",
    "PrivateKeyPath": "keys/es384_private.pem",
    "KeyId":          "<YOUR_KEY_ID>"
  }
}
```

### Configuration Reference

| Key | Required | Description |
|-----|----------|-------------|
| `SmartOnFhir:FhirBaseUrl` | Yes | Base URL of the FHIR server (or proxy) that exposes `.well-known/smart-configuration` |
| `SmartOnFhir:FhirAudience` | Yes | `aud` claim value for standalone token requests (typically the Azure FHIR service URL) |
| `SmartOnFhir:ClientId` | Yes | OAuth client ID for Standalone and EHR launch flows |
| `SmartOnFhir:ClientSecret` | Confidential only | Required for Standalone Confidential and EHR launch |
| `SmartOnFhir:RedirectUri` | Yes | Must match the redirect URI registered in Okta |
| `SmartOnFhir:ContextCacheUrl` | EHR only | API endpoint that stores patient/encounter context and returns a signed launch token |
| `SmartOnFhir:UserIdClaimType` | EHR only | JWT claim name for the user ID (default `uid`) |
| `SmartOnFhir:UserContextClientId` | EHR only | Separate client ID for EHR simulator identity login |
| `SmartOnFhir:UserContextClientSecret` | EHR only | Secret for the EHR simulator identity client |
| `SmartOnFhir:UserContextRedirectUri` | EHR only | Redirect URI for EHR identity callback |
| `BackendServices:FhirBaseUrl` | M2M only | FHIR server for system-level access |
| `BackendServices:Domain` | M2M only | Okta tenant URL (e.g., `https://dev-12345.okta.com`) |
| `BackendServices:AuthServerId` | M2M only | Custom Okta authorization server ID |
| `BackendServices:ClientId` | M2M only | M2M OAuth client ID |
| `BackendServices:Scope` | M2M only | Requested scopes (e.g., `system/*.rs`) |
| `BackendServices:PrivateKeyPath` | M2M only | Path to the ES384 PEM private key (relative or absolute) |
| `BackendServices:KeyId` | M2M only | Key ID matching the public key uploaded to Okta |

---

## Generate the ES384 Private Key

The **Backend Services** flow requires an **ECDSA P-384** key pair. The private key stays on the server; the public key is uploaded to Okta.

### Step 1 — Generate the key pair

```bash
# Generate the EC P-384 private key
openssl ecparam -genkey -name secp384r1 -noout -out es384_private.pem

# Extract the public key (upload this to Okta)
openssl ec -in es384_private.pem -pubout -out es384_public.pem
```

### Step 2 — Place the private key

Copy `es384_private.pem` into the `keys/` directory:

```
SMART-Native-Standalone-EHR-Launch/
  keys/
    es384_private.pem   ← your private key
    README.txt
```

### Step 3 — Upload the public key to Okta

1. In the Okta Admin Console, navigate to your **Backend Services** application.
2. Go to **General** → **Client Credentials** → **Edit**.
3. Select **Public key / Private key** as the client authentication method.
4. Click **Add key** and paste the contents of `es384_public.pem`, or upload the file.
5. Note the **Key ID** assigned by Okta and set it as `BackendServices:KeyId` in `appsettings.json`.

> **Security:** Never commit `es384_private.pem` to source control. The `keys/` directory should be listed in `.gitignore`.

---

## Build and Run

### Option A — .NET CLI

```bash
# Navigate to the solution directory
cd SMART-Native-Standalone-EHR-Launch

# Restore dependencies and build
dotnet build SMART-Native.sln

# Run the web application
dotnet run --project SmartOnFhirDemo.csproj
```

The application will start on **https://localhost:53361**.

### Option B — Visual Studio

1. Open `SMART-Native-Standalone-EHR-Launch/SMART-Native.sln` in Visual Studio 2022+.
2. Set **SmartOnFhirDemo** as the startup project.
3. Press **F5** to build and run.

### Verify the Build

A successful build produces output similar to:

```
Restore complete (0.9s)
  OktaSmartBackend.TokenClient succeeded → …\OktaSmartBackend.TokenClient.dll
  SmartOnFhirDemo succeeded              → …\SmartOnFhirDemo.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Using the Application

Open **https://localhost:53361** in a browser. The dashboard presents all available launch flows.

### Standalone Launch (Public)

1. Select **Standalone — Public** from the launch type options.
2. Choose the FHIR scopes you want to request (e.g., `patient/Patient.rs`, `patient/CarePlan.rs`).
3. Click **Launch**.
4. Authenticate with your Okta credentials and consent to the requested scopes.
5. After redirect, the dashboard displays your access token, refresh token, and granted scopes.
6. Use the **Resource** dropdown to fetch FHIR resources (e.g., Patient, Observation).

### Standalone Launch (Confidential)

Same steps as Public, but select **Standalone — Confidential**. The token exchange includes the `client_secret` server-side. Requires `SmartOnFhir:ClientSecret` to be configured.

### EHR Launch

1. Select **EHR Launch** to reveal the EHR Simulator panel.
2. **Step 1 — Login as EHR User:** Click the login button to authenticate via the lightweight identity flow.
3. **Step 2 — Fill Launch Context:**
   - **ISS** — Pre-filled with the configured FHIR server URL.
   - **Patient ID** — Enter a valid patient identifier (required).
   - **Encounter ID** — Optionally specify an encounter.
   - **Scopes** — Select the scopes to request.
4. Click **Cache & Launch EHR**. The app caches the context, receives a signed `launch` token, and redirects through the OAuth flow.
5. The resulting token includes `patient`, `encounter`, and `need_patient_banner` context fields.

> **Deep-link support:** An EHR system can launch the app directly by navigating to `https://localhost:53361/?iss=<fhir-url>&launch=<token>`.

### Backend Services (M2M)

1. Select **Backend Services**.
2. Click **Launch** — no browser redirect or user login occurs.
3. The server creates a signed JWT assertion (ES384), exchanges it for an access token via `client_credentials`, and displays the M2M token response.
4. Use the **Backend Resource** dropdown to fetch system-level FHIR resources.

---

## Supported FHIR Resources

The following FHIR R4 resource types can be fetched through both user-context and backend flows:

| Resource | Endpoint |
|----------|----------|
| Patient | `GET /Patient` |
| Observation | `GET /Observation` |
| Condition | `GET /Condition` |
| CarePlan | `GET /CarePlan` |
| AllergyIntolerance | `GET /AllergyIntolerance` |
| MedicationRequest | `GET /MedicationRequest` |
| Immunization | `GET /Immunization` |
| Procedure | `GET /Procedure` |
| Encounter | `GET /Encounter` |
| DiagnosticReport | `GET /DiagnosticReport` |

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| **SSL certificate error** on localhost | Dev certificate not trusted | Run `dotnet dev-certs https --trust` |
| **"ClientSecret is required"** error | Chose Confidential launch but secret is empty | Set `SmartOnFhir:ClientSecret` in `appsettings.json` |
| **"launch and iss are required"** error | EHR launch missing context | Complete the EHR Simulator steps before launching |
| **401 Unauthorized** on FHIR fetch | Token expired or insufficient scopes | Re-authenticate and request the needed scopes |
| **Backend token fails** with key error | PEM file missing or wrong path | Verify `keys/es384_private.pem` exists and `BackendServices:PrivateKeyPath` is correct |
| **"kid" mismatch** on M2M flow | Key ID doesn't match Okta | Ensure `BackendServices:KeyId` matches the key ID shown in Okta |
| **SMART config discovery fails** | FHIR server unreachable | Verify `FhirBaseUrl` is accessible and serves `/.well-known/smart-configuration` |
| **Session lost after restart** | In-memory session store | Sessions are in-memory; restart clears all sessions — this is expected for a demo app |

---

## Security Notes

> **Disclaimer:** This is a **sample demonstration application** intended for learning and reference purposes only. It is **not production-ready** as-is. If you plan to use this code as a starting point for a real deployment, ensure you secure all secrets, credentials, and keys according to your organization's security policies and best practices.

---

