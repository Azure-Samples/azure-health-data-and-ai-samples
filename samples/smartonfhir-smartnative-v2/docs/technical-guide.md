# Technical Guide

This document explains how the SMART on FHIR v2 native IdP-agnostic sample works on top of Azure Health Data Services. It covers the deployed components, the role of the gateway in each Identity Provider mode, the discovery mechanism, and the four supported SMART flows.

For step-by-step deployment, see the per-IdP guides:

- [Deploy with Microsoft Entra ID](./deployment-entra.md)
- [Deploy with External IdP (Okta)](./deployment-okta.md)

---

## 1. Architecture overview

```mermaid
flowchart LR
    subgraph Clients["SMART clients"]
        EHR["EHR launch initiator"]
        UApp["User-facing SMART app<br/>(public / confidential)"]
        BApp["Backend service client<br/>(private_key_jwt)"]
    end

    subgraph Azure["Azure subscription · resource group &lt;env&gt;-rg"]
        direction TB
        GW{{"Function App<br/>SMART gateway"}}
        Cache[("Azure Cache (Redis)<br/>optional")]
        KV[("Key Vault<br/>EntraId only")]
        FHIR[("FHIR Service")]
        Insights["App Insights<br/>+ Log Analytics"]
    end

    IdP{{"Identity Provider<br/>Microsoft Entra ID<br/>or External IdP"}}

    EHR -->|launch context| GW
    UApp -->|/authorize and /token| GW
    BApp -->|backend services EntraId| GW
    BApp -.->|backend services ExternalIdp| IdP

    GW -->|discovery| FHIR
    GW -->|/authorize and /token| IdP
    GW <-.->|cache| Cache
    GW -.->|secret + JWKS| KV
    GW -->|traces| Insights
    FHIR -->|traces| Insights

    UApp -->|FHIR resources| FHIR
    BApp -->|FHIR resources| FHIR
    FHIR -->|validate token| IdP
```

| Component | Purpose |
| --- | --- |
| **Azure Health Data Services FHIR Service** | Stores FHIR resources, evaluates SMART scopes natively, and acts as the source of truth for SMART discovery. Validates incoming access tokens against the configured authority and audience. |
| **Azure Function App (SMART gateway)** | Hosts SMART endpoints (`/authorize`, `/token`, `/context-cache`, `/.well-known/*`) and translates SMART concepts into terms the configured IdP understands. The Function App uses a **system-assigned managed identity** for outbound access (FHIR, Key Vault). |
| **Storage Account, App Service Plan** | Function App runtime dependencies (Linux, dotnet-isolated 8). |
| **Application Insights + Log Analytics** | Observability and tracing for both the gateway and the FHIR Service. |
| **Azure Cache (Redis-compatible, optional)** | Distributed EHR launch context cache. When absent, the gateway falls back to in-memory caching (single-instance only). |
| **Backend Services Key Vault** *(EntraId only)* | Stores SMART backend service client registrations as secrets keyed by `client_id`. |

The same components are deployed for both IdP modes; the Key Vault is provisioned only when `IdpType=EntraId`.

---

## 2. Why a gateway is needed

The FHIR Service understands SMART scopes natively (e.g. `patient/Observation.rs`, `user/*.read`) and enforces compartment access using the `fhirUser` claim. It does **not**:

- Host `/authorize` or `/token` endpoints.
- Issue or enrich tokens with SMART launch context (`patient`, `encounter`, `launch`, `fhirUser`).
- Publish a SMART discovery document tailored to the deployment's IdP.

The gateway provides exactly those capabilities. The mechanics of the gateway differ per IdP:

| Concern | Microsoft Entra ID | External IdP (e.g. Okta) |
| --- | --- | --- |
| **SMART scope syntax** | Entra does not understand `patient/*.rs` or `user/*.read`. The gateway translates SMART scopes into Entra application scopes on the way out and re-injects SMART scopes on the way back. | The IdP issues SMART scopes natively. The gateway forwards. |
| **Launch context** (`patient`, `encounter`, `launch`) | Not issued by Entra. The gateway caches launch context during EHR launch and merges it into the token response. | Same — gateway merges from cache. |
| **Backend Services** (`private_key_jwt` + JWKS URL) | Entra does not accept the SMART self-published-JWKS confidential-client model — its asymmetric option is uploaded certificates, not `jwks_url`. The gateway validates the SMART `client_assertion` (ES384 / RS384) against the client's registered JWKS, then exchanges it for an Entra access token using a Key Vault-stored Entra client secret. | Backend services flow **directly to the IdP**. The gateway is bypassed. |
| **Discovery** (`.well-known/smart-configuration`) | Built by the gateway, anchored on the FHIR Service. | Built by the gateway, anchored on the FHIR Service. |

---

## 3. Discovery — the source of truth

There are two discovery endpoints in SMART on FHIR:

- `/.well-known/smart-configuration` — SMART-specific authorization metadata.
- `/metadata` — the FHIR capability statement.

In this sample, the FHIR Service's `.well-known/smart-configuration` is the **single source of truth** for upstream IdP endpoints. The gateway proxies it through `<FunctionBaseUrl>/.well-known/smart-configuration`, rewriting `authorization_endpoint` and `token_endpoint` to itself for the flows that need to be intercepted.

```mermaid
sequenceDiagram
    participant App as SMART App
    participant GW as SMART Gateway
    participant FHIR as FHIR Service

    App->>GW: GET /.well-known/smart-configuration
    GW->>FHIR: GET /.well-known/smart-configuration
    FHIR-->>GW: { authorization_endpoint, token_endpoint, ... }
    GW-->>App: { authorization_endpoint=GW/authorize, token_endpoint=GW/token, ... }
```

The gateway also caches the upstream discovery document so per-request token forwarding does not pay the discovery latency.

---

## 4. Endpoints reference

| Endpoint | Method | Purpose | Mode |
| --- | --- | --- | --- |
| `/api/.well-known/smart-configuration` | GET | SMART discovery (proxied + rewritten) | both |
| `/api/.well-known/openid-configuration` | GET | OIDC discovery (proxied) | both |
| `/api/authorize` | GET | Begins SMART authorization. Translates SMART scopes/launch parameters into the upstream IdP's authorize request and redirects. | both |
| `/api/token` | POST | Exchanges authorization code or client credentials for an access token. Enriches the response with SMART launch context. | both |
| `/api/context-cache` | POST | Accepts launch context from EHR launch initiators and stores it in Redis (or in-memory) keyed by an opaque `launch` token. | both |

---

## 5. Configuration reference

Application settings on the Function App (set automatically by Bicep):

| Setting | Required | Description |
| --- | :---: | --- |
| `AZURE_IdpType` | ✓ | `EntraId` or `ExternalIdp`. |
| `AZURE_TenantId` | EntraId | Microsoft Entra tenant id. |
| `AZURE_Authority_URL` | optional | External IdP authority URL. If blank in External mode, auto-discovered from FHIR `.well-known/smart-configuration`. |
| `AZURE_FhirServerUrl` | ✓ | Base URL of the FHIR Service. |
| `AZURE_FhirAudience` | ✓ | Expected token audience. Defaults to `FhirServerUrl`. |
| `AZURE_FhirResourceAppId` | EntraId | Client ID of the FHIR Resource App registration. |
| `AZURE_ContextAppClientId` | optional | Client ID of the Auth Context Frontend App; when set, restricts callers of `/api/context-cache`. |
| `AZURE_CacheConnectionString` | optional | Redis-compatible connection string. Blank = in-memory cache. |
| `AZURE_BackendServiceKeyVaultStore` | EntraId backend services | Key Vault name (or vault URI) hosting backend client registrations. Empty disables backend services on the gateway. |

---

## 6. Flow — EHR Launch

EHR launch begins inside an EHR session. The EHR posts launch context (selected patient, encounter, etc.) to the gateway, which caches it under an opaque `launch` token and returns that token to the EHR. The EHR redirects the SMART app to its launch URL with the `launch` token; the SMART app discovers the gateway's authorize endpoint, authenticates the user via the configured IdP, and exchanges its code for an access token. The gateway looks up the cached context and merges it into the token response.

### EntraId mode

```mermaid
sequenceDiagram
    participant EHR
    participant App as SMART App
    participant GW as SMART Gateway
    participant Entra as Microsoft Entra ID
    participant FHIR as FHIR Service

    EHR->>GW: POST /api/context-cache (launch context)
    GW-->>EHR: launch token
    EHR->>App: Launch URL with launch token
    App->>GW: GET /api/.well-known/smart-configuration
    GW-->>App: discovery (gateway endpoints)
    App->>GW: GET /api/authorize (SMART scopes + launch)
    GW->>Entra: GET /authorize (translated scopes)
    Entra-->>App: authorization code
    App->>GW: POST /api/token (code)
    GW->>Entra: POST /token (translated)
    Entra-->>GW: access_token (Entra scopes)
    GW->>GW: Resolve launch context from cache
    GW-->>App: access_token + SMART scopes + patient/encounter/launch
    App->>FHIR: GET /Patient/{id}
    FHIR-->>App: 200 OK
```

### ExternalIdp mode

```mermaid
sequenceDiagram
    participant EHR
    participant App as SMART App
    participant GW as SMART Gateway
    participant IdP as External IdP
    participant FHIR as FHIR Service

    EHR->>GW: POST /api/context-cache (launch context)
    GW-->>EHR: launch token
    EHR->>App: Launch URL with launch token
    App->>GW: GET /api/.well-known/smart-configuration
    GW-->>App: discovery (gateway endpoints)
    App->>GW: GET /api/authorize (SMART scopes + launch)
    GW->>IdP: GET /authorize (forwarded)
    IdP-->>App: authorization code
    App->>GW: POST /api/token (code)
    GW->>IdP: POST /token (forwarded)
    IdP-->>GW: access_token (already SMART-shaped)
    GW->>GW: Merge cached launch context
    GW-->>App: access_token + patient/encounter/launch
    App->>FHIR: GET /Patient/{id}
    FHIR-->>App: 200 OK
```

---

## 7. Flow — Standalone Launch

Standalone launch is for applications launched outside an EHR session — typically patient-facing apps. There is no pre-cached launch context, so the gateway relies on the IdP's claim mappings (and the user's selection where applicable) to populate `patient`, `fhirUser`, and similar context.

```mermaid
sequenceDiagram
    participant App as SMART App
    participant GW as SMART Gateway
    participant IdP as Identity Provider
    participant FHIR as FHIR Service

    App->>GW: GET /api/.well-known/smart-configuration
    GW-->>App: discovery (gateway endpoints)
    App->>GW: GET /api/authorize (SMART scopes, PKCE for public clients)
    GW->>IdP: /authorize (translated for Entra, forwarded for External)
    IdP-->>App: authorization code
    App->>GW: POST /api/token (code, code_verifier or client_secret)
    GW->>IdP: /token
    IdP-->>GW: access_token
    GW-->>App: access_token + SMART scopes + fhirUser
    App->>FHIR: GET /Patient/{me}
    FHIR-->>App: 200 OK
```

The same endpoint handles both **public clients** (PKCE, no secret) and **confidential clients with a symmetric secret**. The choice is driven by the request body (presence of `code_verifier` vs `client_secret`). Confidential clients with `private_key_jwt` use the Backend Services flow described next.

---

## 8. Flow — Backend Services

SMART v2 Backend Services use `grant_type=client_credentials` with a `client_assertion` signed by an asymmetric key the client publishes via JWKS. The two IdP modes diverge here:

### EntraId mode — gateway validates and swaps

Entra does not accept arbitrary client-published JWKS as a credential model. The gateway therefore does the SMART-side validation itself, then exchanges to Entra using a stored client secret.

```mermaid
sequenceDiagram
    participant Client as Backend Service Client
    participant GW as SMART Gateway
    participant KV as Key Vault
    participant JWKS as Client JWKS endpoint
    participant Entra as Microsoft Entra ID
    participant FHIR as FHIR Service

    Client->>GW: POST /api/token (client_credentials, client_assertion)
    GW->>KV: Get secret named <client_id>
    KV-->>GW: { value: entra_client_secret, tag: jwks_url }
    GW->>JWKS: GET <jwks_url>
    JWKS-->>GW: keys
    GW->>GW: Validate signature (ES384/RS384), iss=sub=client_id,<br/>aud=token URL, jti replay, lifetime ≤ 5 min
    GW->>Entra: POST /token (client_credentials + entra_client_secret)
    Entra-->>GW: access_token
    GW-->>Client: access_token (SMART system scopes)
    Client->>FHIR: GET /Patient
    FHIR-->>Client: 200 OK
```

### ExternalIdp mode — direct to the IdP

SMART-capable IdPs implement Backend Services natively. The client calls the IdP token endpoint **directly**, using the `token_endpoint` advertised in the FHIR Service's `.well-known/smart-configuration`. The gateway is not involved.

```mermaid
sequenceDiagram
    participant Client as Backend Service Client
    participant FHIR as FHIR Service
    participant IdP as External IdP

    Client->>FHIR: GET /.well-known/smart-configuration
    FHIR-->>Client: token_endpoint = IdP /token
    Client->>IdP: POST /token (client_credentials, client_assertion)
    IdP-->>Client: access_token
    Client->>FHIR: GET /Patient
    FHIR-->>Client: 200 OK
```

> When pointing a backend service client at an `ExternalIdp` deployment, set the client's `BackendServices:FhirBaseUrl` (or equivalent setting) to the **FHIR Service URL**, not the gateway URL, so its discovery resolves the IdP token endpoint.

---

## 9. Resources

- [SMART App Launch v2 Implementation Guide (HL7)](https://hl7.org/fhir/smart-app-launch/)
- [SMART Backend Services](http://hl7.org/fhir/smart-app-launch/backend-services.html)
- [Azure Health Data Services FHIR Service — SMART on FHIR (proxy)](https://learn.microsoft.com/azure/healthcare-apis/fhir/smart-on-fhir)
- [Microsoft Entra ID — `acceptMappedClaims`](https://learn.microsoft.com/azure/active-directory/develop/reference-app-manifest#acceptmappedclaims-attribute)

[Back to README](../README.md)
