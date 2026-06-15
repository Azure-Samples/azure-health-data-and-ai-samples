# SMART on FHIR v2 — Native client application

A full-featured ASP.NET Core 8 demonstration of all four **SMART on FHIR v2** authorization flows against a SMART proxy fronting **Azure Health Data Services**. The sample is **IdP-agnostic** — the same code runs against **Microsoft Entra ID** or **Okta**; switching between them is a single config flag plus an IdP-side recipe.

| Flow | Grant type | Client type |
|---|---|---|
| Standalone (public) | `authorization_code` + PKCE | Public |
| Standalone (confidential) | `authorization_code` + PKCE + `client_secret` | Confidential |
| EHR launch | `authorization_code` + PKCE + `launch` context | Confidential |
| Backend Services (M2M) | `client_credentials` + `private_key_jwt` (ES384) | Machine-to-machine |

---

## Prerequisites

| Requirement | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **8.0** or later |
| [OpenSSL](https://www.openssl.org/) | recent — for ES384 key generation |
| FHIR R4 server | Azure Health Data Services or compatible |
| SMART proxy | The `smartonfhir-smart-v2-external-idp` sample, deployed and reachable |
| IdP | Microsoft Entra ID **or** Okta |

> [!NOTE]
> This sample is the **client** half. The proxy that translates SMART scopes into IdP scopes lives in a separate repository — deploy that first using its `docs/deployment-entra.md` or `docs/deployment-okta.md` instructions.

---

## Repository layout

```
SMART-Client-Application/
├── SMART-Native-Standalone-EHR-Launch/   ← ASP.NET Core 8 MVC app (the dashboard)
│   ├── Controllers/
│   │   ├── HomeController.cs             ← dashboard + session
│   │   └── SmartController.cs            ← /login, /callback, /usercontext/*, /ehr-context-launch, /fhir
│   ├── Services/
│   │   ├── SmartConfigService.cs         ← discovers .well-known/smart-configuration
│   │   ├── AuthService.cs                ← authorize + token exchange (interactive flows)
│   │   ├── BackendTokenService.cs        ← M2M token client wrapper
│   │   └── FhirService.cs                ← Bearer-authenticated FHIR calls
│   ├── appsettings.json                  ← placeholders only (this file)
│   ├── appsettings.Development.json.example ← template for local overrides (gitignored once renamed)
│   └── keys/                             ← place es384_private.pem here (gitignored)
└── SmartBackendServices.TokenClient/     ← reusable class library — IdP-agnostic
    ├── SmartBackendTokenClient.cs        ← signs ES384 JWT, requests token at any endpoint
    ├── SmartBackendJwtAssertion.cs       ← assertion builder
    └── SmartBackendEndpoints.cs          ← endpoint helpers
```

---

## Documentation

| Doc | What it covers |
|---|---|
| [docs/configuration.md](docs/configuration.md) | Every `appsettings.json` key, when it's required, and where each value comes from per IdP. **Read this first.** |
| [docs/idp-setup-entra.md](docs/idp-setup-entra.md) | Five Entra app registrations (Standalone Public, Standalone Confidential, EHR Launch, Backend Services, User Context). |
| [docs/idp-setup-okta.md](docs/idp-setup-okta.md) | Five Okta apps wired against the SMART custom authorization server provided by the proxy sample. |
| [docs/generate-es384-key.md](docs/generate-es384-key.md) | Generate the ES384 key pair for Backend Services and install it on Okta (JWKS) or Entra (Key Vault JWK). |
| [docs/flows.md](docs/flows.md) | What each launch does end-to-end: sequence diagrams, expected token claims, first FHIR call. |
| [docs/troubleshooting.md](docs/troubleshooting.md) | Common errors with cause and fix. |

---

## Quick start

1. **Deploy the proxy.** Follow the deployment guide in the proxy sample (`smartonfhir-smart-v2-external-idp`). Note the Function App URL and FHIR audience URL.
2. **Set up your IdP.** Pick one and follow:
   - [docs/idp-setup-entra.md](docs/idp-setup-entra.md), or
   - [docs/idp-setup-okta.md](docs/idp-setup-okta.md).
3. **Generate the ES384 key pair** for Backend Services per [docs/generate-es384-key.md](docs/generate-es384-key.md). Drop the private key at `SMART-Native-Standalone-EHR-Launch/keys/es384_private.pem`.
4. **Configure local overrides:**
   ```powershell
   cd SMART-Native-Standalone-EHR-Launch
   Copy-Item appsettings.Development.json.example appsettings.Development.json
   ```
   Open `appsettings.Development.json` and fill in the values from steps 1 and 2. (`appsettings.Development.json` is gitignored, so secrets stay on your machine.) Use [docs/configuration.md](docs/configuration.md) as the field-by-field reference.
5. **Build and run:**
   ```powershell
   dotnet run --project SmartOnFhirDemo.csproj
   ```
6. Browse to **`https://localhost:53361`** and pick a launch from the dashboard. See [docs/flows.md](docs/flows.md) for what each one does.

---

## How the IdP-agnostic switch works

Every IdP-aware bit of behaviour is driven from a **single setting**: `SmartOnFhir:IdpType` (`EntraId` or `ExternalIdp`). It selects:

- which EHR-simulator token (`id_token` for Entra, `access_token` for Okta) is used to authenticate the call to the proxy's context-cache, and
- which JWT claim (`oid` for Entra, `sub` for Okta) carries the user identity that keys the cache entry.

Token endpoints are **never** hard-coded — they are discovered from `<FhirBaseUrl>/.well-known/smart-configuration` on every flow. Adding a new IdP would require nothing more than another `IdpType` value and the matching claim convention.

For details, see [docs/configuration.md → IdpType](docs/configuration.md#smartonfhiridptype).

---

## Disclaimer

This is a **sample demonstration application** intended for learning and reference. It is **not production-ready** as-is. If you plan to use this code as a starting point for a real deployment, ensure you secure all secrets, credentials, and keys according to your organization's security policies and best practices.
