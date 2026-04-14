# Direct backend auth proof kit

This folder is a **manual proof kit** for testing whether SMART on FHIR backend-services auth can work with either:

- **Microsoft Entra External**, or
- a standard **Microsoft Entra tenant**

without the current Azure Function + Key Vault exchange pattern.

It does **not** modify the existing `(g)(10)` SMART implementation. It gives you a repeatable way to:

1. request a backend token directly from the identity provider,
2. inspect the resulting JWT claims, and
3. call the FHIR service directly with that token.

## Repo assets this proof reuses

- `samples/fhir-aad-entra-external/` for the Entra External + FHIR trust direction
- `samples/client-assertion-generator/` if you want to generate a `client_assertion` and JWKS from a certificate

## Prerequisites

Before running the proof, you need:

1. A FHIR service configured to trust the authority you want to test.
   - For **Entra External**, this usually means `authenticationConfiguration.smartIdentityProviders`.
   - For a standard **Entra tenant**, this can be the primary authority already used by the FHIR service, or another trusted configuration if you are testing a separate authority.
2. A backend client registration in the identity provider being tested.
3. A token endpoint URL for that tenant.
4. The FHIR audience or scope expected by the resource app.
5. A `client_assertion` for the backend client if you are validating a SMART-style `private_key_jwt` request.

## Option A: run the PowerShell proof script

The script requests a token, decodes the access token, highlights risky claims, and optionally performs a direct FHIR read.

```powershell
pwsh ./Test-BackendDirectAuth.ps1 `
  -TokenEndpoint "https://<your-tenant>.ciamlogin.com/<your-tenant-id>/oauth2/v2.0/token" `
  -ClientId "<backend-client-id>" `
  -FhirAudience "api://<your-fhir-resource-app-id>" `
  -ClientAssertionFile "./client-assertion.txt" `
  -FhirBaseUrl "https://<your-fhir-service>.fhir.azurehealthcareapis.com" `
  -OutputDirectory "./artifacts"
```

### Useful parameters

- `-Scope` if you want to provide the exact scope instead of deriving `/.default` from `-FhirAudience`
- `-ClientAssertion` if you want to paste the assertion inline instead of loading it from a file
- `-FhirRelativePath` if you want to test a different read path than `Patient?_count=1`

### Script outputs

If `-OutputDirectory` is provided, the script writes:

- `token-response.json`
- `access-token-header.json`
- `access-token-claims.json`
- `proof-summary.json`
- `fhir-result.json` when a FHIR request is attempted

The same script also works for a standard Entra tenant by swapping the token endpoint:

```powershell
pwsh ./Test-BackendDirectAuth.ps1 `
  -TokenEndpoint "https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token" `
  -ClientId "<backend-client-id>" `
  -FhirAudience "api://<your-fhir-resource-app-id>" `
  -ClientAssertionFile "./client-assertion.txt" `
  -FhirBaseUrl "https://<your-fhir-service>.fhir.azurehealthcareapis.com" `
  -OutputDirectory "./artifacts-entra"
```

## Option C: run the minimal SMART-aware shim proof

If you want to test the recommended direct-trust architecture instead of the current Entra secret-exchange bridge, use:

```bash
python3 ./Run-MinimalSmartShim.py \
  --issuer "http://127.0.0.1:8765"
```

The shim serves:

- `/.well-known/openid-configuration`
- `/jwks.json`
- `POST /token`

### What the shim proof does

- loads backend-client registrations from `./registrations/*.json`
- validates SMART `private_key_jwt` assertions for `client_credentials`
- supports inbound `RS384` and `ES384`
- enforces:
  - `iss = sub = client_id`
  - `aud = <shim issuer>/token`
  - `kid` matching
  - `jku` = registered `jwksUri` when present
  - one-time-use `jti`
- issues a shim-signed access token that can be used for Azure FHIR direct-trust experiments

### Registration format

Use `registrations/inferno-client.template.json` as the template for a backend client registration.

Important fields:

- `clientId`: exact SMART `client_id` / `iss` / `sub` value expected from the client assertion
- `jwksUri`: client public JWKS endpoint
- `allowedScopes`: scopes the shim will allow for that client
- `fhirAudience`: audience claim to emit in the shim-issued token
- `fhirUser`: optional proof claim to emit for Azure FHIR trust experiments

### Running a local token request

Once the shim is running and a client registration is in place:

```bash
curl -X POST "http://127.0.0.1:8765/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "grant_type=client_credentials" \
  --data-urlencode "client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer" \
  --data-urlencode "client_assertion=$(cat ./client-assertion.txt)" \
  --data-urlencode "scope=system/*.r"
```

### Important proof limitations

- this is a **proof shim**, not a production authorization server
- replay protection is local to the proof service state file
- the issued token claim shape still needs to be validated against your Azure FHIR configuration
- the shim signs its **outbound** tokens with its own key; inbound client assertions remain SMART `RS384` / `ES384`

### Azure FHIR direct-trust example

For a direct-trust proof, Azure FHIR needs to trust the shim authority and allowlist the backend client IDs that the shim emits in `azp` / `appid`.

Example shape:

```json
{
  "properties": {
    "authenticationConfiguration": {
      "authority": "https://login.microsoftonline.com/<primary-tenant-id>",
      "audience": "https://<your-fhir-service>.fhir.azurehealthcareapis.com",
      "smartProxyEnabled": false,
      "smartIdentityProviders": [
        {
          "authority": "https://<your-shim-host>",
          "applications": [
            {
              "clientId": "<registered-backend-client-id>",
              "audience": "api://<your-fhir-resource-app-id>",
              "allowedDataActions": ["Read"]
            }
          ]
        }
      ]
    }
  }
}
```

For this proof, the important claim mapping is:

- shim token `iss` -> shim `authority`
- shim token `aud` -> `applications[].audience`
- shim token `azp` or `appid` -> `applications[].clientId`
- shim token `scp` -> SMART/backend scope
- shim token `fhirUser` -> optional proof claim to satisfy Azure FHIR identity-provider expectations

## Option B: run the REST Client files

Open one of these files in VS Code with the REST Client extension, fill in the placeholders, send the token request, then send the FHIR request:

- `direct-backend-auth.http` for **Entra External**
- `direct-backend-auth-entra.http` for a standard **Entra tenant**

This is useful when you want a quick manual run before using the script.

## Inferno RSA JWKS upload experiment

If you want to test whether Entra can trust Inferno's RSA public key once it is wrapped in an uploadable certificate, use:

```bash
python3 ./Prepare-InfernoJwksUploadArtifacts.py --output-dir ./inferno-jwks-artifacts
```

This script:

1. fetches Inferno's JWKS,
2. selects the RSA key,
3. converts that JWK into a PEM public key,
4. wraps the public key in an X.509 certificate, and
5. writes both PEM and `.cer` artifacts for upload tests.

To upload the generated cert to an Entra app registration:

```bash
python3 ./Prepare-InfernoJwksUploadArtifacts.py \
  --output-dir ./inferno-jwks-artifacts \
  --app-id "<backend-client-id>" \
  --upload
```

Equivalent Azure CLI upload command:

```bash
az ad app credential reset \
  --id "<backend-client-id>" \
  --cert "@./inferno-jwks-artifacts/inferno-rsa-upload-cert.cer" \
  --append
```

Important:

- this tests whether Entra will accept **registered public key material** derived from Inferno's RSA JWK,
- it does **not** prove that Entra will accept Inferno's full SMART-style assertion format,
- and it does **not** make Entra a native `jku`/JWKS-dereferencing authorization server.

## Minimal custom OIDC trust issuer experiment

If you want a very small experiment that publishes Inferno's JWKS through a custom OIDC discovery document for trust testing, use:

```bash
python3 ./Prepare-InfernoJwksUploadArtifacts.py \
  --output-dir ./inferno-jwks-artifacts \
  --oidc-issuer-url "https://<your-public-host>/inferno-oidc-test"
```

This writes a static package under:

- `inferno-jwks-artifacts/inferno-oidc-issuer/.well-known/openid-configuration`
- `inferno-jwks-artifacts/inferno-oidc-issuer/jwks.json`
- `inferno-jwks-artifacts/inferno-oidc-issuer/federated-credential-sample.json`

Useful options:

- `--oidc-jwks-mode selected-rsa` to publish only the selected Inferno RSA key (default)
- `--oidc-jwks-mode full-source` to publish the full fetched Inferno JWKS
- `--federated-subject "<expected-sub-claim>"` to prefill the sample federated credential JSON
- `--federated-audience "<audience>"` to override the default `api://AzureADTokenExchange`

This experiment is intended to answer a narrower question:

> Can Microsoft Entra resolve a custom OIDC issuer and its hosted JWKS when that issuer publishes Inferno-derived key material?

Important:

- this package is **static OIDC discovery + JWKS hosting only**
- it does **not** generate tokens
- it does **not** make Inferno's SMART `client_assertion` automatically compatible with workload identity federation
- the external token still needs `iss`, `sub`, and `aud` values that exactly match the federated credential definition
- Inferno SMART assertions usually use SMART-specific claim values, so a claim-shape mismatch may still block the exchange even if Entra can read the JWKS

## Parallel experiment: standard Entra tenant

Use the standard Entra experiment when you want to answer a narrower question:

> If the backend client is registered directly in a normal Entra tenant, and authenticates with `private_key_jwt` or a certificate-backed client assertion, can it get a token that works directly against FHIR?

Suggested setup for that parallel test:

1. Register the FHIR resource application in the Entra tenant.
2. Register a backend confidential client in the same tenant.
3. Upload the backend certificate/public key to that client registration.
4. Grant the backend client the FHIR application permission you want to test.
5. Request a token from `https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token`.
6. Compare the resulting token claims and FHIR behavior against the Entra External run.

## Claims checklist

The first manual proof should capture whether the issued access token contains claims that line up with Azure Health Data Services FHIR expectations:

- `aud` matches the application audience configured for the trusted identity provider
- `azp` or `appid` matches the backend client that FHIR is configured to trust
- `scp` contains the expected SMART/backend scope information
- `roles` is present or absent, and whether it appears instead of `scp`

The most important risk to resolve is whether the direct token shape from either provider is acceptable to FHIR and the relevant Inferno backend-services tests.

## Suggested proof sequence

1. Generate or obtain the backend client assertion.
2. Request a token directly from the chosen identity provider.
3. Decode and save the token claims.
4. Attempt a direct FHIR read with the issued access token.
5. Record:
   - token endpoint result,
   - access token claims,
   - FHIR response code/body,
   - any mismatch with expected SMART backend behavior.

## Go / no-go signal

Treat the proof as a **go** only when all of the following are true:

- The chosen identity provider issues a token successfully for the backend client.
- The token claims match what FHIR validation requires.
- Direct FHIR access succeeds with that token.
- The claim shape looks compatible with the backend-services behaviors you need to demonstrate in Inferno.

If you run both Entra External and standard Entra in parallel, compare:

- `iss`
- `aud`
- `azp` or `appid`
- `scp`
- `roles`
- direct FHIR response status
- any difference in admin consent or app-registration requirements

If the proof fails, capture whether the blocker is:

- token issuance,
- token claims,
- FHIR identity-provider configuration,
- or Inferno compatibility.

## If the proof succeeds

The next step should be a **separate branch** containing a very small handoff artifact for the vendor team, such as:

- a script-first proof package, or
- a tiny console sample that requests the token and calls FHIR directly.

That follow-up branch should stay decoupled from `SMARTCustomOperations.AzureAuth`.
