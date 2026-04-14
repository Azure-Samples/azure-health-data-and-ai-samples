# SMART-aware shim implementation options

## Goal

Describe what the "shim" actually is if we promote the current broker concept into a real SMART-aware issuer that Azure FHIR trusts directly.

This note focuses on one design question:

> Can the shim just be static file hosting for OIDC metadata and JWKS, or does it need to be a real authorization service?

## Short answer

Your idea is **partly right**, but only for the **issuer-facing public artifacts**:

- `/.well-known/openid-configuration`
- the shim's own `jwks_uri`
- optional static registration/config files

Those pieces can absolutely live in static storage.

What **cannot** be static is the SMART backend authentication path itself, because the shim still needs to:

1. accept `POST /token`,
2. determine which backend client is calling,
3. load that client's registered `jwks_uri` or JWKS,
4. validate the inbound `private_key_jwt`,
5. enforce `aud`, `kid`, `jku`, `exp`, and `jti`,
6. apply the client's allowed scopes / data actions,
7. issue a new access token from the shim issuer.

So the real split is:

- **public issuer metadata can be static**
- **client authentication and token issuance must be dynamic**

## How this differs from my earlier thought

Your proposed shape sounds like:

```text
Static storage
  ├─ /.well-known/openid-configuration
  ├─ /jwks.json
  └─ maybe some client registration files
```

My earlier mental model was:

```text
SMART-aware shim
  ├─ public issuer metadata (can be static)
  ├─ token endpoint (dynamic)
  ├─ client registration store
  ├─ replay protection store
  ├─ client JWKS fetch/cache logic
  └─ token signing / issuance
```

So the difference is not really philosophical. It is mostly about **where the line is**:

- your idea covers the **issuer shell**
- my idea includes the **authorization logic behind the shell**

## Why static-only is not enough

If the shim is only static files, it can prove:

- the shim has an issuer URL,
- the shim publishes signing keys,
- Azure FHIR can discover and trust the shim authority.

But it cannot do the most important SMART work:

- validate Inferno's `private_key_jwt`
- bind a request to a specific registered `client_id`
- fetch and validate the client's JWKS
- enforce one-time-use `jti`
- mint a token for that specific client

That means static-only hosting is **an issuer facade**, not a full backend-services auth solution.

## Recommended implementation options

## Option 1 - Static metadata + minimal token function

### What it is

Use static hosting for:

- `/.well-known/openid-configuration`
- shim `jwks.json`
- optional per-client registration documents

Use a very small dynamic component for:

- `/token`
- client registration lookup
- client assertion validation
- replay detection
- token issuance

### Example shape

```text
Storage Account / Blob / Static Web App
  ├─ /.well-known/openid-configuration
  ├─ /jwks.json
  └─ /registrations/{client_id}.json   (optional, private or admin-managed)

Azure Function / App Service
  └─ POST /token
       ├─ lookup client registration
       ├─ fetch client jwks_uri / JWKS
       ├─ validate SMART assertion
       ├─ enforce jti replay protection
       └─ issue shim token
```

### Why this is attractive

- closest to your "simple shim" idea
- low operational footprint
- keeps public OIDC artifacts simple and inspectable
- dynamic logic stays limited to one endpoint

### Main drawback

- still needs a real auth function behind `/token`
- still needs secure signing-key handling
- still needs nonce/replay storage

### Recommendation

This is the **best minimal viable design**.

## Option 2 - File-backed registration + token service

### What it is

Same as Option 1, but make the registration store explicit and admin-managed.

For example, store one config file per client:

```json
{
  "clientId": "inferno-backend-client",
  "jwksUri": "https://inferno.healthit.gov/suites/custom/smart_stu2/.well-known/jwks.json",
  "allowedScopes": ["system/*.rs"],
  "allowedDataActions": ["Read"],
  "status": "active"
}
```

### Example shape

```text
Private storage/config repo
  └─ registrations/
       ├─ client-a.json
       ├─ client-b.json
       └─ client-c.json

Token service
  └─ loads config for requested client_id
```

### Why this is attractive

- simple admin model
- easy to reason about
- no separate database required at first
- good fit for manual onboarding

### Main drawback

- file-based updates are clumsy at scale
- concurrent updates / audit trail become harder
- replay detection still needs a dynamic store

### Recommendation

Good for an early prototype or low-volume manual onboarding.

## Option 3 - Full SMART authorization service

### What it is

A more complete service with:

- dynamic client registration storage
- optional admin API or portal
- token issuance
- replay store
- JWKS cache/refresh logic
- richer policy and telemetry

### Example shape

```text
Shim API
  ├─ GET /.well-known/openid-configuration
  ├─ GET /jwks.json
  ├─ POST /token
  └─ POST /admin/clients

Backing services
  ├─ client registry
  ├─ replay/nonce store
  ├─ audit log
  └─ signing key store
```

### Why this is attractive

- clean long-term architecture
- better control and auditability
- easier to support multiple clients and policies
- better path to production hardening

### Main drawback

- biggest implementation and operational burden
- more moving parts than needed for a first proof

### Recommendation

Best long-term design if this becomes a supported product feature, but too heavy for the first proof.

## Option 4 - Static public issuer + broker back to Entra/External

### What it is

Keep the shim public surface small, but instead of issuing its own final token for FHIR, the shim validates the SMART assertion and then exchanges into Entra / Entra External.

### Why it exists

This is basically a cleaner version of the current sample:

- the shim handles SMART auth
- Entra still issues the final token

### Why it is weaker than direct trust

- still a token exchange pattern
- still tied to Entra/External constraints
- does not fully remove the design you were trying to replace

### Recommendation

Only worth doing if direct FHIR trust turns out to be blocked.

## Best way to think about the shim

The shim is **not just an OIDC metadata host**.

It is really two things:

1. **An issuer surface**
   - openid configuration
   - issuer JWKS
   - stable authority URL

2. **A SMART backend authorization engine**
   - per-client registration
   - client assertion validation
   - replay protection
   - token issuance

The public issuer surface can be mostly static.
The authorization engine cannot.

## Suggested prototype path

If the goal is to keep the shim as simple as possible while still proving the architecture, the best first step is:

1. **Option 1** as the prototype
2. with **Option 2-style file-backed client registration**

That gives:

- static OIDC metadata
- static shim JWKS
- manual client onboarding
- one small dynamic `/token` component
- no dependency on Entra token exchange

## Proposed first implementation boundary

### Static

- `/.well-known/openid-configuration`
- `/jwks.json`
- optional registration files

### Dynamic

- `POST /token`
- registration lookup
- client JWKS fetch/cache
- replay store
- token issuance

## Bottom line

If by "shim" we mean:

> a tiny issuer that only serves static metadata and keys

then it is **not enough**.

If by "shim" we mean:

> a tiny SMART-aware token service with static public metadata

then yes — that is very close to what I think is the best minimal path.
