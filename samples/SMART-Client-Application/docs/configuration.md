# Configuration reference

All settings live in [`SMART-Native-Standalone-EHR-Launch/appsettings.json`](../SMART-Native-Standalone-EHR-Launch/appsettings.json). For local development, copy [`appsettings.Development.json.example`](../SMART-Native-Standalone-EHR-Launch/appsettings.Development.json.example) to `appsettings.Development.json` and override values there — that file is gitignored so secrets stay on your machine.

The application is **IdP-agnostic**. The single switch [`SmartOnFhir:IdpType`](#smartonfhiridptype) tells it whether the proxy is fronting Microsoft Entra ID or an external IdP (Okta) and is used to pick the correct token type and user-id claim when caching EHR launch context.

---

## Settings at a glance

### `SmartOnFhir` section — interactive flows (Standalone + EHR launch)

| Key | Required | Description |
|---|---|---|
| `FhirBaseUrl` | yes | Base URL of the deployed Auth Function App (the SMART proxy). Used as the issuer for `.well-known/smart-configuration` discovery. |
| `EhrIssDefault` | EHR only | Pre-fills the **ISS** field on the EHR-launch panel. Usually the same value as `FhirBaseUrl`. |
| `ClientId` | yes | Client ID of the SMART app registration used by the **Standalone (public)**, **Standalone (confidential)**, and **EHR launch** buttons. See [idp-setup-entra.md](idp-setup-entra.md) / [idp-setup-okta.md](idp-setup-okta.md). |
| `ClientSecret` | confidential / EHR | Client secret. Sent only when the flow is **Standalone Confidential** or **EHR Launch**. Leave blank for public-PKCE-only operation. |
| `RedirectUri` | yes | Must match the redirect URI registered with the IdP. Default: `https://localhost:53361/callback`. |
| `FhirAudience` | yes | Audience for the FHIR service. You can get this from FHIR Service -> Settings -> Authentication -> Audience field. |
| `ContextCacheUrl` | EHR only | Full URL of the proxy's `context-cache` endpoint — usually `<FhirBaseUrl>/api/context-cache`. |
| `IdpType` | yes | `EntraId` or `ExternalIdp`. See below. |
| `UserContextClientId` | EHR only | Client ID of the OIDC-only **User Context** app used in the EHR simulator. |
| `UserContextClientSecret` | EHR only | Client secret of the User Context app. |
| `UserContextRedirectUri` | EHR only | Default: `https://localhost:53361/usercontext/callback`. |

### `BackendServices` section — machine-to-machine flow

| Key | Required | Description |
|---|---|---|
| `FhirBaseUrl` | yes | Base URL used for SMART discovery on the backend flow. Usually the same as `SmartOnFhir:FhirBaseUrl`. |
| `ClientId` | yes | Client ID of the **Backend Services** app registration. |
| `Scope` | yes | Scope to request. Typical value: `system/*.rs`. |
| `PrivateKeyPath` | yes | Path to the ES384 PEM private key. Relative to the project root (e.g. `keys/es384_private.pem`) or absolute. |
| `KeyId` | yes | The **Key ID (kid)** that matches the public key in your IdP's JWKS (Okta Admin Console assigns; for Entra, this is the `kid` you wrote into the JWK uploaded to Key Vault). See [generate-es384-key.md](generate-es384-key.md). |

> [!NOTE]
> No `Domain`, `AuthServerId`, or `UserIdClaimType` keys exist in the current code. Token endpoints are discovered from `.well-known/smart-configuration`, and the user-id claim is selected from `IdpType`.

---

## `SmartOnFhir:IdpType`

The single source of truth for IdP-aware behaviour. It is read in [`SmartController.EhrContextLaunch`](../SMART-Native-Standalone-EHR-Launch/Controllers/SmartController.cs) and drives:

| `IdpType` | User Context token used for cache | User-id claim read |
|---|---|---|
| `EntraId` | `id_token` | `oid` (stable Entra object id; identical across `id_token` / `access_token`) |
| `ExternalIdp` | `access_token` | `sub` (Okta custom auth servers emit a different `sub` in `id_token` vs `access_token`, so the access token's `sub` is the one the proxy reads) |

The proxy uses the same `IdpType` to extract the same claim from the EHR launch's access token, so the cache key round-trips. Setting this incorrectly means the EHR launch will fail with `Could not find launch context for user`.

---

## Per-IdP value sources

Use the table below to map each setting to where the value comes from for each IdP.

| Setting | Microsoft Entra ID | Okta |
|---|---|---|
| `SmartOnFhir:FhirBaseUrl` | Function App URL deployed by the proxy sample | Function App URL deployed by the proxy sample |
| `SmartOnFhir:ClientId` | Application (client) ID of **smart-client-standalone-public** / **-confidential** / **-ehr-launch** | Client ID of the matching Okta SMART app |
| `SmartOnFhir:ClientSecret` | Client secret of the confidential/EHR-launch app | Client secret of the matching Okta SMART app |
| `SmartOnFhir:FhirAudience` | FHIR service URL (Azure Health Data Services workspace) | FHIR service URL (Azure Health Data Services workspace) |
| `SmartOnFhir:IdpType` | `EntraId` | `ExternalIdp` |
| `SmartOnFhir:UserContextClientId` | App ID of **smart-client-user-context** (Entra) | Client ID of `smart-user-context` (Okta) |
| `SmartOnFhir:UserContextClientSecret` | Secret of the same | Secret of the same |
| `BackendServices:ClientId` | App ID of **smart-client-backend-services** | Client ID of `smart-backend-services` (Okta API Services app) |
| `BackendServices:KeyId` | `kid` of the JWK uploaded as a Key Vault secret with the `jwks_url` tag | Key ID assigned by Okta when you uploaded the public key |

For the IdP-side recipe, see:

- Entra: [idp-setup-entra.md](idp-setup-entra.md)
- Okta: [idp-setup-okta.md](idp-setup-okta.md)

For the ES384 key pair (Backend Services), see [generate-es384-key.md](generate-es384-key.md).

---

## Local development pattern

```text
appsettings.json                      → checked in, contains placeholders only
appsettings.Development.json          → gitignored, contains your real values
appsettings.Development.json.example  → checked in, template you copy from
keys/es384_private.pem                → gitignored, your private key
```

> [!TIP]
> When running with `dotnet run`, ASP.NET Core layers `appsettings.Development.json` over `appsettings.json` automatically because `ASPNETCORE_ENVIRONMENT=Development`. You only need to fill in keys you want to override.

---

[Back to the main README](../README.md)
