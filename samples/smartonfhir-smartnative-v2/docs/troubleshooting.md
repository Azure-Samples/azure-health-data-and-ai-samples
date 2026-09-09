# Troubleshooting

This guide collects the most common issues you may hit when deploying or running the SMART on FHIR v2 native IdP-agnostic sample, organized by where the problem typically surfaces.

---

## Diagnostics first

Before drilling into individual issues, gather these signals — they make the cause obvious in most cases.

### Application Insights

The deployment provisions Application Insights for the Function App and the FHIR Service. Use the **Failures** and **Performance** blades, and the **Logs** experience for ad‑hoc KQL.

Useful queries:

```kusto
// Recent gateway failures
requests
| where cloud_RoleName endswith "auth-func"
| where success == false
| order by timestamp desc
| project timestamp, name, resultCode, customDimensions, operation_Id
```

```kusto
// Trace logs around a specific operation_Id (replace value)
union traces, exceptions, dependencies, requests
| where operation_Id == "<operation-id>"
| order by timestamp asc
```

### Decode an access token

Paste the token at [jwt.ms](https://jwt.ms/) and check:

- `iss` — should match the configured authority (Entra `https://login.microsoftonline.com/<tenant-id>/v2.0` or your External IdP authority).
- `aud` — should match the `FhirAudience` of the FHIR Service.
- `fhirUser` — must be present for SMART user flows; should resolve to a real FHIR resource.
- `scope` — should contain SMART-shaped scopes (e.g. `patient/Observation.rs`).
- `patient`, `encounter`, `launch` — only required for EHR launch flows.

### Inspect SMART discovery

Compare what the gateway advertises with what the FHIR Service advertises:

```http
GET <FunctionBaseUrl>/.well-known/smart-configuration
GET <FhirUrl>/.well-known/smart-configuration
```

The gateway version should rewrite `authorization_endpoint` and `token_endpoint` to itself; the FHIR version should expose the upstream IdP endpoints. If they disagree on `issuer` or `audience`, that is usually the source of token validation problems downstream.

### Check the FHIR identity provider whitelist

In the Azure Portal, open the FHIR Service → **Authentication** → **SMART on FHIR identity providers**. Confirm:

- The **authority** matches your IdP exactly (no trailing slash drift, correct `/oauth2/v2.0` segment for Entra v2).
- The **audience** matches `FhirAudience`.
- The **applications** array lists every confidential or backend service `client_id` you expect to call the FHIR Service.

---

## Deployment issues

### `azd up` fails with insufficient permissions

The deployer needs both Azure RBAC permissions to create the resource group and resources, and (for `IdpType=EntraId`) Microsoft Entra rights to create app registrations. Check that the signed-in account has:

- **Owner** on the subscription (or a custom role with `Microsoft.Resources/*`).
- Microsoft Entra **Application Administrator** or higher for app registration steps.

### `azd up` fails with resource provider not registered

Register the missing provider on the subscription:

```powershell
az provider register --namespace Microsoft.HealthcareApis --wait
az provider register --namespace Microsoft.Web --wait
az provider register --namespace Microsoft.KeyVault --wait
```

### `IdpType` not set

If `azd up` keeps prompting for `IdpType` despite earlier `azd env set` calls, confirm the active environment:

```powershell
azd env list
azd env get-values
```

If you have multiple environments (`smartnativeokta`, `smartnativegeneric`, etc.), select the right one with `azd env select <name>` before re-running.

### PowerShell script execution policy blocks the helper scripts

If you see `Script cannot be loaded. The script is not digitally signed.` when running scripts under `scripts/`, allow them for the current process:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

This change does not persist beyond the current PowerShell session.

---

## Authentication and authorization issues

### `fhirUser` claim missing in the access token

This is the most common cause of `401`/`403` against the FHIR Service.

**For Microsoft Entra ID:**

- Confirm the directory extension was created: it should be named `extension_<FhirResourceAppId>_fhirUser` on each user.
- Confirm the Enterprise Application's **Single sign-on → Attributes & Claims** maps the directory extension to the `fhirUser` claim. See [Set fhirUser Claim Mapping](./ad-apps/set-fhir-user-mapping.md).
- Confirm `acceptMappedClaims=true` in the FHIR Resource App manifest.
- Confirm each test user has the directory extension populated (`Add-FhirUserInfoToUser.ps1`).

**For External IdPs:**

- Confirm the IdP's authorization server emits a `fhirUser` claim in access tokens.
- Confirm the user's profile in the IdP has the FHIR URL stored against the right attribute, and that the attribute is wired into the access-token claim mapping.
- Decode the access token at [jwt.ms](https://jwt.ms/) and check the claim spelling exactly — it must be `fhirUser`.

### `401 Unauthorized` from the FHIR Service

Decode the access token and check, in order:

1. **`iss` (issuer)** matches the FHIR Service's configured authority.
2. **`aud` (audience)** matches `FhirAudience`.
3. **`client_id`** is in the FHIR identity provider whitelist (`applications[]`).
4. **Token has not expired** (`exp`).
5. **`fhirUser`** is present (for user flows).

If all five look correct and you still see 401, restart the failing client — caches sometimes hold onto stale discovery documents.

### `403 Forbidden` from the FHIR Service

The token is accepted but the user is not authorized. Check:

- The test user has the **FHIR SMART User** role assigned on the FHIR Service.
- The token's `scope` claim contains a SMART scope that authorizes the operation (e.g. `patient/Observation.rs` for reading Observation).
- The `fhirUser` claim resolves to a real resource that exists in the FHIR Service.

### `not getting correct scopes`

If the access token contains broader scopes than your test selected, ensure no admin consent has been granted for the application. **Never grant admin consent (AllPrincipals) for `user_impersonation` on the FHIR Resource App when Entra ID is the IdP** — admin consent overrides the per-user grants the gateway relies on.

---

## Runtime issues — gateway

### `400 Bad Request` on `/api/context-cache`

The caller does not match the expected `client_id` or audience. Check:

- The **caller's access token** has `aud` equal to the FHIR audience (or to the gateway's audience configuration).
- `AZURE_ContextAppClientId` matches the **client_id of the EHR launch initiator** that posted the context.
- The token is fresh (not expired).

Audience validation accepts the FHIR Service URL, the FHIR audience, and the gateway base URL. If your caller uses a different audience, add it to the IdP's audience configuration so it matches one of these.

### Cache miss / `need_patient_banner: true` with no patient

The token response was issued without launch context being merged in. Causes:

- The EHR launch initiator never posted to `/api/context-cache` for that `launch` token.
- The gateway is using **in-memory caching** (no `AZURE_CacheConnectionString` set) and the request landed on a different Function App instance from the one that received the context post.

For multi-instance deployments, set `AZURE_CacheConnectionString` to a Redis-compatible connection string.

### Backend services returns `Backend services proxy not applicable for IdpType 'ExternalIdp'`

Backend services in External IdP mode go **directly to the IdP**, not through the gateway. Reconfigure your backend client so its discovery URL resolves to the **FHIR Service**'s `.well-known/smart-configuration` (so it picks up the IdP token endpoint), not the gateway's.

### Backend services `client_assertion` rejected (Entra mode)

Common rejections from the gateway, in priority order:

| Error | Cause | Fix |
| --- | --- | --- |
| `Unsupported client_assertion_type` | Wrong `client_assertion_type` form value | Use `urn:ietf:params:oauth:client-assertion-type:jwt-bearer` |
| `client_assertion header is missing kid` | JWT header has no `kid` | Sign with a key whose `kid` matches an entry in your published JWKS |
| `client_assertion must satisfy iss=sub=client_id` | `iss` and `sub` claims differ from `client_id` form value | Set all three to the SMART `client_id` |
| `client_assertion lifetime must be between 1 and 300 seconds` | `exp - iat > 300` | Reduce assertion lifetime to ≤ 5 minutes |
| `client_assertion jku does not match registered jwks_url` | `jku` header set, but does not match the secret's `jwks_url` tag in Key Vault | Either remove `jku` from the JWT header, or align it with the registered `jwks_url` |
| Signature verification fails | Signing key not in the published JWKS, or algorithm mismatch | Use ES384 or RS384 only; rotate JWKS endpoint to include the signing key |

If the assertion validates but the swap to Entra fails, the Key Vault secret value (the Entra client secret) is wrong or expired. Update the secret in the backend services Key Vault (its name is in `.azure/<env-name>/.env` as `BackendServiceKeyVaultName`) — the secret **name** is the SMART `client_id`, the **value** is the Entra client secret.

### Function App returns 500 on `/api/token`

Check Application Insights for the underlying exception. Frequent causes:

- `AZURE_FhirServerUrl` or `AZURE_FhirAudience` blank or malformed.
- `AZURE_TenantId` blank in `EntraId` mode.
- The Function App managed identity does not have **Key Vault Secrets User** on the backend services Key Vault (only relevant for Entra backend services).

---

## Client-side issues

### Client uses the wrong `BackendServices:FhirBaseUrl`

When testing backend services from a client app:

- **Entra mode**: point at the **gateway** (`<FunctionBaseUrl>`). The client's discovery resolves the gateway's `/api/token` (which performs assertion validation and Entra swap).
- **External mode**: point at the **FHIR Service** (`<FhirUrl>`). The client's discovery resolves the IdP token endpoint directly.

Mixing these causes the client to call the wrong endpoint and fail with confusing errors.

### Discovery URL is the wrong one

If your client builds its own discovery URL (e.g. `<authority>/.well-known/openid-configuration`), make sure the authority is the IdP's authority — not the gateway's. The gateway proxies SMART discovery, not OIDC discovery.

For Entra v2, the discovery URL is:

```
https://login.microsoftonline.com/<tenant-id>/v2.0/.well-known/openid-configuration
```

> A trailing `/oauth2/v2.0` on the authority is **wrong** — the discovery URL needs the base authority + `/v2.0/.well-known/openid-configuration`.

---

## Data issues

### Sample data load fails with 401

The user account running `Load-ProfilesData.ps1` needs the **FHIR Data Contributor** role on the FHIR Service. The deployment grants this role to the deployer principal (`AZURE_PRINCIPAL_ID`) automatically. If you run the script as a different user, assign the role manually:

1. Open the FHIR Service in the Azure Portal.
2. Go to **Access control (IAM) → Add → Add role assignment**.
3. Select **FHIR Data Contributor** and assign to the user.

### Sample data load fails with 400 on the audience parameter

For External IdPs, `Load-ProfilesData.ps1` requires `-FhirAudience`. Get it from:

- `azd env get-values | findstr FhirAudience`, or
- FHIR Service → **Authentication** → **Audience** in the Azure Portal.

Do not copy the value from inside `Application1` — copy the top-level **Audience** field.

---

## Going back

[Back to README](../README.md) · [Back to Deployment Overview](./deployment.md)
