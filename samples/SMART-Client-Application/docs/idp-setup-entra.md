# IdP setup — Microsoft Entra ID

This page walks you through registering the **five** Microsoft Entra ID applications the client sample needs, then maps each one back to the corresponding [`appsettings.json`](../SMART-Native-Standalone-EHR-Launch/appsettings.json) value.

> [!IMPORTANT]
> The proxy sample (the one you deployed alongside this client) already ships a detailed Entra app-registration recipe in `docs/ad-apps/smart-client-app-registrations.md`. That doc covers the **first four** apps in the table below (Standalone Public, Standalone Confidential, EHR Launch, Backend Services). This page references those instructions and adds the fifth app (User Context) which is unique to the client sample's EHR simulator.

---

## What you will produce

| # | App registration | Purpose | Public client? | Used by `appsettings.json` key |
|---|---|---|---|---|
| 1 | `smart-client-standalone-public` | Standalone launch (PKCE only) | yes | `SmartOnFhir:ClientId` (when running the public flow) |
| 2 | `smart-client-standalone-confidential` | Standalone launch (PKCE + secret) | no | `SmartOnFhir:ClientId` + `ClientSecret` (when running the confidential flow) |
| 3 | `smart-client-ehr-launch` | EHR launch (PKCE + secret + `launch` scope) | no | `SmartOnFhir:ClientId` + `ClientSecret` (when running the EHR-launch flow) |
| 4 | `smart-client-backend-services` | Backend Services (M2M, ES384 + `private_key_jwt`) | no | `BackendServices:ClientId` + `KeyId` |
| 5 | `smart-client-user-context` | OIDC-only login that authenticates the simulated EHR user before launch context is cached | no | `SmartOnFhir:UserContextClientId` + `UserContextClientSecret` |

Apps **1–4** are SMART-on-FHIR clients in the strict sense — the proxy translates their SMART scopes (`patient/*.rs`, `system/*.rs`, …) into Entra-formatted scopes. App **5** is a plain OIDC login that the **client sample** uses to identify the EHR user; it does not call the FHIR service.

---

## 1. Standalone Public, Standalone Confidential, EHR Launch, Backend Services

Follow the proxy sample's app-registration guide for these four apps:

It tells you, per app:

- **Redirect URIs** — for this client sample, set `https://localhost:53361/callback` on apps 1–3.
- **Client authentication** — public for app 1; client secret for apps 2, 3; certificate (JWKS) via Key Vault for app 4. The Key Vault entry uses the `jwks_url` tag the proxy reads.
- **API permissions** — dot-notation SMART scopes (`patient.Patient.rs`, `system.Patient.rs`, …) exposed by the FHIR service app registration.
- **`fhirUser` mapping** — required for SMART user-level scopes. Detailed in `docs/ad-apps/set-fhir-user-mapping.md` of the proxy sample.

When you finish those four, record the values you'll paste into [`appsettings.json`](../SMART-Native-Standalone-EHR-Launch/appsettings.json) — see [Hand-off values](#hand-off-values) at the bottom of this page.

---

## 2. App 5 — `smart-client-user-context`

The EHR simulator's **Step 1 — Login as EHR User** button calls `/usercontext/login`, which kicks off a plain OIDC `authorization_code` flow against the proxy. The token from this flow is **only** used to authenticate the call to the proxy's `context-cache` endpoint; it never reaches the FHIR service.

### A. Register the app

1. In the Azure portal, go to **Microsoft Entra ID → App registrations → New registration**.
2. Name: `smart-client-user-context`.
3. Supported account types: **Accounts in this organizational directory only** (single-tenant).
4. Redirect URI:
   - Platform: **Web**
   - URI: `https://localhost:53361/usercontext/callback`
5. **Register**.

### B. Add a client secret

1. **Certificates & secrets → Client secrets → New client secret**.
2. Description: `local-dev`. Expiry: per your policy.
3. **Copy the secret value immediately** — it is shown only once. You'll paste it into `SmartOnFhir:UserContextClientSecret`.

### C. API permissions

Only OpenID Connect scopes are needed. The default `User.Read` permission added by Entra is fine; you can leave it as-is.

> [!NOTE]
> This app does **not** need any of the FHIR service's exposed scopes. The client sample requests only `openid` for this flow (see [`AuthService.BuildUserContextAuthorizationRequestAsync`](../SMART-Native-Standalone-EHR-Launch/Services/AuthService.cs)).

### D. Token configuration (recommended)

Optional, but produces nicer payloads:

1. **Token configuration → Add optional claim → ID → `email`**, `preferred_username`.
2. Accept the prompt to grant the implicit Microsoft Graph permission, if shown.

These claims surface in the dashboard once the user is signed in.

### E. Authentication

Confirm:

- **Implicit grant and hybrid flows** — both checkboxes cleared (the sample uses authorization-code only).
- **Allow public client flows** — **No**.

---

## Hand-off values

After completing all five registrations, fill in [`appsettings.json`](../SMART-Native-Standalone-EHR-Launch/appsettings.json) (or your local `appsettings.Development.json`):

```jsonc
{
  "SmartOnFhir": {
    "FhirBaseUrl":    "https://<your-auth-func>.azurewebsites.net",
    "EhrIssDefault":  "https://<your-auth-func>.azurewebsites.net",
    "ClientId":       "<app 1, 2, or 3 — depending on which flow you're running>",
    "ClientSecret":   "<empty for app 1; secret for apps 2 & 3>",
    "RedirectUri":    "https://localhost:53361/callback",
    "FhirAudience":   "https://<workspace>-<fhir>.fhir.azurehealthcareapis.com",
    "ContextCacheUrl":"https://<your-auth-func>.azurewebsites.net/api/context-cache",
    "IdpType":        "EntraId",
    "UserContextClientId":     "<app 5 client id>",
    "UserContextClientSecret": "<app 5 secret>",
    "UserContextRedirectUri":  "https://localhost:53361/usercontext/callback"
  },
  "BackendServices": {
    "FhirBaseUrl":    "https://<your-auth-func>.azurewebsites.net",
    "ClientId":       "<app 4 client id>",
    "Scope":          "system/*.rs",
    "PrivateKeyPath": "keys/es384_private.pem",
    "KeyId":          "<kid of the JWK in your Key Vault secret>"
  }
}
```

> [!TIP]
> If you want to demo all four interactive launches without re-editing config, register **one** Entra Web app and reuse it across apps 1–3 (PKCE works for both confidential and public-style requests; just leave `ClientSecret` blank in config when you want to test the public flow). The four-app split shown here mirrors how a real distributed system would scope its credentials.

For the ES384 key pair used by app 4, see [generate-es384-key.md](generate-es384-key.md).

---

[Back to the main README](../README.md) · [Configuration reference](configuration.md) · [Okta setup](idp-setup-okta.md)
