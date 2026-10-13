# ONC (g)(10) SMART on FHIR v2 — Native IdP-Agnostic Sample

The [Azure Health Data Services FHIR Service](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/overview) supports Substitutable Medical Applications and Reusable Technologies ([SMART on FHIR](https://docs.smarthealthit.org/)) by implementing the key server-side behaviors required for SMART clients to securely access FHIR data using OAuth 2.0 and OpenID Connect. 

This sample demonstrates **SMART App Launch v2** authorization flows on top of the **Azure Health Data Services FHIR Service**. It is IdP-agnostic: the same gateway and infrastructure support either **Microsoft Entra ID** or an **external identity provider** such as Okta.

The gateway is an orchestration layer between the identity provider, a clinical system such as an EHR, SMART applications, and the FHIR service. It supplies SMART-specific behavior that general-purpose identity providers do not provide natively.

This sample provides a **SMART gateway Function App** that:

1. Translates SMART concepts (scopes, launch parameters, PKCE flows) into terms the configured Identity Provider understands.
2. Enriches token responses with SMART launch context (cached during EHR launch).
3. Provides **SMART v2 Backend Services** support (`client_credentials` + `private_key_jwt`) where the IdP cannot do this natively.
4. Provides token revocation enforcement through the gateway FHIR proxy.
5. In Entra mode, provides a consent picker, an EHR launch initiator, and authenticated FHIR Bulk Data export proxying.

### Why the gateway is needed per IdP

| Concern | Microsoft Entra ID | External IdP (e.g. Okta) |
| --- | --- | --- |
| **SMART scope syntax** (`patient/*.rs`) | Not understood by Entra — gateway translates to/from Entra application scopes. | Understood natively by SMART-capable IdPs. Gateway only forwards. |
| **SMART launch context** (`patient`, `encounter`, `launch`) | Not issued by Entra — gateway injects cached context into the token response. | Same — gateway injects cached context into the token response. |
| **Backend Services (`private_key_jwt` + JWKS URL)** | Entra does not accept the SMART self-published-JWKS confidential-client model. Gateway validates the SMART `client_assertion` (ES384 / RS384) against the client's registered JWKS, then swaps to a Key Vault-stored Entra client secret. | Handled natively by the IdP. Backend service clients call the IdP token endpoint **directly**; the gateway is bypassed. |
| **Discovery (`.well-known/smart-configuration`)** | Built by the gateway, anchored on the FHIR Service. | Built by the gateway, anchored on the FHIR Service. |

The Entra path therefore requires more gateway logic and a small Key Vault. The External-IdP path is intentionally minimal.

---

## Supported flows (SMART App Launch v2)

- EHR Launch
- Standalone Launch — Public Client (PKCE)
- Standalone Launch — Confidential Client (symmetric secret)
- Backend Services (`client_credentials` + `private_key_jwt`, ES384 / RS384)
- Refresh token exchange (`offline_access`)
- Token revocation through the gateway proxy
- FHIR Bulk Data group export in Entra mode

---

## Components deployed

The same Azure components are deployed for both IdP modes, with one mode-specific addition for Entra ID.

| Component | EntraId | ExternalIdp | Purpose |
| --- | :---: | :---: | --- |
| Azure Health Data Services FHIR Service | ✓ | ✓ | Stores FHIR resources, evaluates SMART scopes, sources SMART discovery |
| Azure Function App (SMART gateway) | ✓ | ✓ | Hosts `/authorize`, `/token`, `/context-cache`, `/.well-known/*` proxy |
| App Service Plan (Linux, dotnet-isolated 8) | ✓ | ✓ | Hosts the Function App |
| Storage Account | ✓ | ✓ | Function App runtime storage |
| Application Insights + Log Analytics | ✓ | ✓ | Function App observability and tracing |
| Dedicated export Storage Account | ✓ | ✓ | Stores FHIR Bulk Data export output. The gateway export proxy is enabled only in Entra mode. |
| External Redis-compatible cache (optional; not provisioned) | opt | opt | Stores EHR launch context and revocation state across multiple Function App instances. Supply `AZURE_CacheConnectionString`; otherwise state is process-local. |
| Backend Services Key Vault | ✓ | — | Stores backend service client registrations (Entra secret + `jwks_url` tag) |

> The Key Vault is only deployed when `IdpType=EntraId` because backend services on External IdPs talk to the IdP directly and never touch the gateway.
>
> The deployment does not create a Redis resource. If distributed state is required, provision a Redis-compatible service separately and set `AZURE_CacheConnectionString`.

---

## Architecture

A high-level component view: clients call the gateway for SMART endpoints, the gateway resolves the upstream IdP token endpoint from the FHIR Service's `.well-known/smart-configuration`, and the FHIR Service validates access tokens directly against the configured authority/audience.

```mermaid
flowchart LR
    subgraph Clients["SMART clients"]
        EHR["EHR launch initiator"]
        UApp["User-facing SMART app<br/>(public / confidential)"]
        BApp["Backend service client<br/>(private_key_jwt)"]
    end

    subgraph Azure["Azure subscription · resource group &lt;env&gt;-rg"]
        direction TB
        GW{{"Function App<br/>SMART gateway<br/>/authorize · /token<br/>/context-cache · /well-known/*"}}
        Cache[("External Redis-compatible cache<br/>optional · not provisioned")]
        KV[("Key Vault<br/>EntraId only<br/>backend client registrations")]
        Export[("Storage Account<br/>bulk export output")]
        FHIR[("Azure Health Data Services<br/>FHIR Service")]
        Insights["App Insights<br/>+ Log Analytics"]
    end

    IdP{{"Identity Provider<br/>Microsoft Entra ID<br/>or External IdP (e.g. Okta)"}}

    EHR -->|launch context| GW
    UApp -->|SMART /authorize and /token| GW
    BApp -->|backend services EntraId| GW
    BApp -.->|backend services ExternalIdp| IdP

    GW -->|discovery| FHIR
    GW -->|/authorize and /token| IdP
    GW <-.->|cache| Cache
    GW -.->|secret + JWKS| KV
    FHIR -->|bulk export output| Export
    GW -->|authenticated export reads| Export
    GW -->|traces| Insights

    UApp -->|FHIR resources| FHIR
    BApp -->|FHIR resources| FHIR
    FHIR -->|validate token| IdP
```

For the per-flow sequence diagrams (EHR launch, standalone, backend services) see the [Technical Guide](docs/technical-guide.md).

---

## Prerequisites

- An Azure subscription with permission to create resources.
- The following tools installed locally:
  - [Git](https://git-scm.com/)
  - [Azure CLI 2.51+](https://learn.microsoft.com/cli/azure/install-azure-cli)
  - [Azure Developer CLI 1.9+](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
  - [.NET 8 SDK](https://learn.microsoft.com/dotnet/core/sdk)
  - [PowerShell 7+](https://learn.microsoft.com/powershell/scripting/install/installing-powershell)
- Identity Provider access:
  - **Microsoft Entra ID**: tenant admin to create app registrations and Microsoft Graph directory extensions.
  - **External IdP (Okta)**: an Okta Authorization Server with admin access to register applications and configure SMART claim mappings.
- Two test user accounts in the chosen IdP (one *Patient* persona, one *Practitioner* persona).

Detailed prerequisites are listed in the per-IdP deployment guides.

---

## Get started

Pick the deployment guide that matches your identity provider.

- **[Deployment Overview](docs/deployment.md)** — start here to choose an IdP path
- **[Deploy with Microsoft Entra ID](docs/deployment-entra.md)**
- **[Deploy with External IdP (Okta)](docs/deployment-okta.md)**

Reference material:

- **[Technical Guide](docs/technical-guide.md)** — endpoint behaviors, sequence diagrams per flow, and the role of the gateway in each mode
- **[Troubleshooting](docs/troubleshooting.md)**
- **[Sample Data](docs/sample-data.md)**

---

## Project layout

| Path | Purpose |
| --- | --- |
| `src/SMARTCustomOperations.AzureAuth/` | Function App source (.NET 8 isolated worker) |
| `infra/main.bicep` | Subscription-scope deployment entry point |
| `infra/core/` | Reusable modules (FHIR, monitoring, function host, Key Vault, export storage, and role assignments) |
| `infra/app/authCustomOperation.bicep` | Function App resource and configuration |
| `scripts/` | PowerShell utilities (data load, app registration, claim mapping) |
| `docs/` | Documentation set (this file's siblings) |

---
