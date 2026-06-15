# Troubleshooting

A field guide for the most common errors you'll see when running the four launches. Symptoms are grouped by **where** the failure surfaces (browser, dashboard, FHIR call) so you can find them quickly.

---

## Discovery / startup

| Symptom | Likely cause | Fix |
|---|---|---|
| `SmartOnFhir:FhirBaseUrl is not configured.` on app start. | The setting is missing from [`appsettings.json`](../SMART-Native-Standalone-EHR-Launch/appsettings.json) (or `appsettings.Development.json`). | Set it to the proxy Function App URL. See [configuration.md](configuration.md). |
| `404` on `/.well-known/smart-configuration` when the dashboard first loads. | The Function App is not running, or the wrong URL is configured. | `curl https://<your-auth-func>.azurewebsites.net/.well-known/smart-configuration` — fix until you get a JSON document with `authorization_endpoint` and `token_endpoint`. |
| `token_endpoint not found in SMART configuration.` in the Backend Services flow. | The proxy returned discovery metadata but the field is empty. | Check the proxy's deployment — its `well-known` controller must produce both `authorization_endpoint` and `token_endpoint`. |

---

## Authorize step (`/login` → IdP login screen)

| Symptom | Likely cause | Fix |
|---|---|---|
| Browser stops at `redirect_uri does not match`. | The redirect URI you registered in the IdP differs from the one the sample sends. | Make the IdP redirect URI exactly `https://localhost:53361/callback` (or `/usercontext/callback` for the User Context app). Watch trailing slashes. |
| IdP error: `invalid_scope` / `scope_not_granted`. | The authorization server hasn't been granted the SMART scope you ticked on the dashboard. | Add the scope to your authorization server (Okta) or the API app registration's exposed scopes (Entra). See [idp-setup-entra.md](idp-setup-entra.md) / [idp-setup-okta.md](idp-setup-okta.md). |
| IdP login screen appears but redirects back to the app with `state mismatch`. | A second tab started a parallel flow and overwrote the session, or sessions are being stripped (e.g. running over plain HTTP). | Use only one tab; make sure you're on `https://localhost:53361`. |
| `prompt=login consent` does not actually re-prompt. | The IdP has `Allow auto-grant` or a remembered consent. | Either revoke the user's grant in the IdP admin console, or test in a fresh in-private browser window. |

---

## Token step (`/callback` → token exchange)

| Symptom | Likely cause | Fix |
|---|---|---|
| `invalid_grant` | The PKCE `code_verifier` doesn't match the `code_challenge` sent on `/authorize`. Usually means session was lost between requests. | Confirm cookies are surviving (HTTPS + a single tab); try again. |
| `unauthorized_client` | You sent `client_secret` to a public-only client, or omitted it where the client is configured as confidential. | Match the client type in the IdP to which dashboard button you clicked (see [idp-setup-entra.md](idp-setup-entra.md) / [idp-setup-okta.md](idp-setup-okta.md)). |
| Confidential flow returns `access_token` but the proxy didn't include `patient` / `encounter`. | You ran **EHR Launch** but step 1 of the simulator (Login as EHR User) was skipped or wiped before launching. | Run step 1 fresh, then click **Cache & Launch EHR**. The first leg must complete so the proxy has context to inject. |
| EHR launch authorize returns `Could not find launch context for user`. | The user-id claim the client read for the cache key disagrees with the user-id claim the proxy reads from the EHR launch's access token. | This is almost always `SmartOnFhir:IdpType` set wrong: must be `EntraId` for Entra and `ExternalIdp` for Okta. See [configuration.md → IdpType](configuration.md#smartonfhiridptype). |

---

## Backend Services (M2M)

| Symptom | Likely cause | Fix |
|---|---|---|
| `BackendServices:ClientId, KeyId, and PrivateKeyPath must be set in configuration.` | One of those keys is empty in [`appsettings.json`](../SMART-Native-Standalone-EHR-Launch/appsettings.json). | Fill them in per [configuration.md](configuration.md#backendservices-section--machine-to-machine-flow). |
| `Private key file not found: keys/es384_private.pem`. | The key isn't in the project's content root. | Place it at `SMART-Native-Standalone-EHR-Launch/keys/es384_private.pem` (or set `BackendServices:PrivateKeyPath` to an absolute path). |
| `invalid_client` from the IdP. | The `kid` in `BackendServices:KeyId` does not match the `kid` of the JWK in the IdP's JWKS, or the public key in the IdP doesn't match the private key on disk. | Re-run the [generate-es384-key.md](generate-es384-key.md) sanity check. Make sure you didn't upload a stale public key. |
| `invalid_grant: client_assertion is invalid`. | The assertion's `aud` doesn't match what the IdP expects. The sample sets `aud` from SMART discovery, so this almost always means the discovery URL is wrong. | Curl `<FhirBaseUrl>/.well-known/smart-configuration` and check the `token_endpoint` — that exact URL must be a token endpoint your IdP accepts assertions for. |
| Token exchange succeeds but `GET /Patient` returns 403. | The Backend Services app's authorization server has not been granted `system/*.rs` (Okta) or the corresponding dot-notation scope (Entra). | Grant the scope on the auth server / API permissions and retry. |

---

## FHIR calls

| Symptom | Likely cause | Fix |
|---|---|---|
| 401 on every FHIR call. | The access token expired (default 1 hour) or wasn't sent. | Re-run the launch. The dashboard does **not** auto-refresh access tokens — for refresh-token flow, examine the response and call `/token` with `grant_type=refresh_token`. |
| 401 with `WWW-Authenticate: Bearer error="invalid_token"`. | The audience claim doesn't match the FHIR service. | Verify `SmartOnFhir:FhirAudience` is the actual FHIR service URL (Azure Health Data Services workspace URL), not the Function App URL. |
| 403 `Forbidden`. | Token is valid but the user / system principal lacks RBAC on the FHIR service, **or** the SMART scope you got is too narrow. | Check the `scope` claim in the access token; check the IAM assignments on the Azure Health Data Services workspace. |
| 200 but resource list is empty. | Patient-context scope is correct but the cached `patient` id doesn't have any matching resources. | Use the **Backend Services** flow to query the same resource type without context to confirm data exists, then fix the test patient. |

---

## Sanity scripts

When in doubt, run these from a terminal on the same machine as the client sample:

```bash
# 1. Is the proxy reachable?
curl -i https://<your-auth-func>.azurewebsites.net/.well-known/smart-configuration

# 2. Decode the access token shown on the dashboard.
# Paste the token at https://jwt.ms — confirm aud, scope, fhirUser, and (Okta) sub / (Entra) oid.

# 3. Reproduce the M2M token request from the command line.
# Replace TOKEN_ENDPOINT, CLIENT_ID, ASSERTION with the values from the dashboard.
curl -s -X POST "$TOKEN_ENDPOINT" \
  -d grant_type=client_credentials \
  -d client_id="$CLIENT_ID" \
  -d client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer \
  -d client_assertion="$ASSERTION" \
  -d scope=system/*.rs
```

If those three return what you expect but the sample still fails, capture the dashboard's **Raw token response** panel and the proxy's Application Insights traces — almost every remaining failure is visible there.

---

[Back to the main README](../README.md) · [Configuration reference](configuration.md) · [Flows](flows.md)
