# IdP setup — Okta

This page walks you through registering the **five** Okta applications the client sample needs, then maps each one back to the corresponding [`appsettings.json`](../SMART-Native-Standalone-EHR-Launch/appsettings.json) value.

> [!IMPORTANT]
> The proxy sample (the one you deployed alongside this client) already ships a complete Okta tenant recipe in `docs/external-idp/okta-configuration.md`. That document covers the **authorization server**, **audience**, **scopes**, **claims** (including the `appid` claim required by the FHIR service), the **`fhirUser` user attribute**, **test users**, **access policies**, and **OIDC discovery**. Configure those tenant-level pieces first; this page only covers the per-app registrations on top of that.

---

## What you will produce

| # | Okta application | Type | Purpose | Used by `appsettings.json` key |
|---|---|---|---|---|
| 1 | `smart-standalone-public` | SPA | Standalone launch (PKCE only) | `SmartOnFhir:ClientId` (when running the public flow) |
| 2 | `smart-standalone-confidential` | Web | Standalone launch (PKCE + secret) | `SmartOnFhir:ClientId` + `ClientSecret` (confidential flow) |
| 3 | `smart-ehr-launch` | Web | EHR launch (PKCE + secret + `launch` scope) | `SmartOnFhir:ClientId` + `ClientSecret` (EHR-launch flow) |
| 4 | `smart-backend-services` | API Services | Backend Services (M2M, ES384 + `private_key_jwt`) | `BackendServices:ClientId` + `KeyId` |
| 5 | `smart-user-context` | Web | OIDC-only login that authenticates the simulated EHR user before launch context is cached | `SmartOnFhir:UserContextClientId` + `UserContextClientSecret` |

> [!TIP]
> If you want to keep the tenant minimal, you can collapse apps 1, 2, 3 into a single Web app — Okta lets one Web app run both PKCE-only and PKCE+secret token requests, and `launch` scope is requested at runtime. The five-app split below mirrors how a real distributed system would scope its credentials, and it is what the proxy sample assumes.

---

## Common settings

For all five apps, choose:

- **Sign-on policy** → the policy attached to the SMART **custom authorization server** you set up via the proxy sample's `docs/external-idp/okta-configuration.md` page.
- **Assignments** → assign the test users / groups you created in that same page.

> [!NOTE]
> Test against your custom authorization server's `/oauth2/<authServerId>` endpoints, **not** the default Okta authorization server. The proxy sample exposes its `.well-known/smart-configuration` so the discovery URLs are wired up automatically.

---

## 1. `smart-standalone-public` — SPA

| Setting | Value |
|---|---|
| Application type | **OIDC — Single-Page Application (SPA)** |
| Grant types | Authorization Code |
| PKCE | Required |
| Sign-in redirect URI | `https://localhost:53361/callback` |
| Sign-out redirect URI | (leave blank) |
| Scopes (granted on the auth server) | `openid`, `profile`, `fhirUser`, `offline_access`, `launch/patient`, `patient/*.rs` |

After creating, copy the **Client ID** to `SmartOnFhir:ClientId` (when you run the public flow).

---

## 2. `smart-standalone-confidential` — Web

| Setting | Value |
|---|---|
| Application type | **OIDC — Web Application** |
| Grant types | Authorization Code |
| PKCE | Required (still used alongside the secret) |
| Sign-in redirect URI | `https://localhost:53361/callback` |
| Client authentication | **Client secret** |

Copy the **Client ID** and **Client secret** to `SmartOnFhir:ClientId` / `SmartOnFhir:ClientSecret` (when you run the confidential flow).

---

## 3. `smart-ehr-launch` — Web

Same as **smart-standalone-confidential** — Web app with PKCE + secret. The only difference is what the client requests at `/authorize`:

- The client sample includes an opaque `launch` parameter when this flow is invoked (see [`AuthService.BuildAuthorizationRequestAsync`](../SMART-Native-Standalone-EHR-Launch/Services/AuthService.cs)). Make sure your authorization server's scopes include `launch` and `launch/patient`.

Copy the **Client ID** and **Client secret** to `SmartOnFhir:ClientId` / `SmartOnFhir:ClientSecret` when you switch to the EHR-launch flow.

---

## 4. `smart-backend-services` — API Services

| Setting | Value |
|---|---|
| Application type | **OIDC — API Services** (machine-to-machine) |
| Grant types | Client Credentials |
| Client authentication | **Public key / Private key** (signed JWT, `private_key_jwt`) |
| Algorithm | **ES384** |
| Scopes (granted on the auth server) | `system/*.rs` (and any narrower `system/<Resource>.rs` you want to test) |

After creating:

1. Generate the ES384 key pair (see [generate-es384-key.md](generate-es384-key.md)).
2. Upload the **public key** to this app under **General → Client Credentials → Public Keys**.
3. Note the **Key ID (kid)** Okta assigns — paste it into `BackendServices:KeyId`.
4. Copy the **Client ID** to `BackendServices:ClientId`.

> [!IMPORTANT]
> The token endpoint is **not** read from configuration any more — `BackendTokenService` discovers it from `.well-known/smart-configuration` on `BackendServices:FhirBaseUrl`. The old `Domain` and `AuthServerId` settings are no longer used.

---

## 5. `smart-user-context` — Web

The EHR simulator's **Step 1 — Login as EHR User** button calls `/usercontext/login`, which kicks off a plain OIDC `authorization_code` flow. The token is **only** used to authenticate the call to the proxy's `context-cache` endpoint; it never reaches the FHIR service.

| Setting | Value |
|---|---|
| Application type | **OIDC — Web Application** |
| Grant types | Authorization Code |
| PKCE | Required |
| Sign-in redirect URI | `https://localhost:53361/usercontext/callback` |
| Scopes (granted on the auth server) | `openid` only |
| Client authentication | **Client secret** |

> [!NOTE]
> The client sample requests **only** `openid` for this flow — no `aud`, no SMART scopes (see [`AuthService.BuildUserContextAuthorizationRequestAsync`](../SMART-Native-Standalone-EHR-Launch/Services/AuthService.cs)). Keep the scope grant minimal here so this app can never accidentally mint a FHIR-bearing token.

Copy the **Client ID** and **Client secret** to `SmartOnFhir:UserContextClientId` / `SmartOnFhir:UserContextClientSecret`.

---

## Hand-off values

```jsonc
{
  "SmartOnFhir": {
    "FhirBaseUrl":    "https://<your-auth-func>.azurewebsites.net",
    "EhrIssDefault":  "https://<your-auth-func>.azurewebsites.net",
    "ClientId":       "<app 1, 2, or 3 client id — depending on flow>",
    "ClientSecret":   "<empty for app 1; secret for apps 2 & 3>",
    "RedirectUri":    "https://localhost:53361/callback",
    "FhirAudience":   "https://<workspace>-<fhir>.fhir.azurehealthcareapis.com",
    "ContextCacheUrl":"https://<your-auth-func>.azurewebsites.net/api/context-cache",
    "IdpType":        "ExternalIdp",
    "UserContextClientId":     "<app 5 client id>",
    "UserContextClientSecret": "<app 5 secret>",
    "UserContextRedirectUri":  "https://localhost:53361/usercontext/callback"
  },
  "BackendServices": {
    "FhirBaseUrl":    "https://<your-auth-func>.azurewebsites.net",
    "ClientId":       "<app 4 client id>",
    "Scope":          "system/*.rs",
    "PrivateKeyPath": "keys/es384_private.pem",
    "KeyId":          "<key id Okta assigned when you uploaded the public key>"
  }
}
```

> [!IMPORTANT]
> Set `IdpType` to **`ExternalIdp`** for Okta. See [configuration.md](configuration.md#smartonfhiridptype) for why this matters — it changes which token (`id_token` vs `access_token`) and which claim (`oid` vs `sub`) the EHR simulator uses to identify the user.

---

[Back to the main README](../README.md) · [Configuration reference](configuration.md) · [Entra setup](idp-setup-entra.md)
