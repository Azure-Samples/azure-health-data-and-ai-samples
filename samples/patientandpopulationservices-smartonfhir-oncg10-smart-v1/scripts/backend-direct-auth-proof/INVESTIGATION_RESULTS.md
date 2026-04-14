# SMART backend auth investigation results

## Summary

This investigation tested whether SMART on FHIR asymmetric client authentication for Inferno and backend-services clients could work directly with Microsoft Entra, without the current Azure Function + Key Vault exchange pattern.

### Bottom line

- **Manual public-key registration is allowed by the SMART spec.**
- **Microsoft Entra can store manually registered public-key material if it is uploaded as an X.509 certificate.**
- **However, Entra did not accept Inferno's actual client assertion after uploading a certificate derived from Inferno's RSA JWKS key.**
- This is strong evidence that **JWKS-only registration does not map cleanly to Entra's certificate-based client assertion validation model**.

## Questions investigated

1. Can a direct backend-services flow work against **standard Entra** or **Entra External** without the current token-exchange bridge?
2. Can Inferno's registration model (`jwks_uri`, `RS384` / `ES384`) be represented in Entra?
3. If SMART allows **manual registration** of a client's public key, can that manual step be implemented by uploading equivalent key material to Entra?

## SMART spec findings

From the SMART App Launch STU2 `client-confidential-asymmetric` profile:

- SMART does **not** require a specific registration protocol.
- A client may register its public key with the authorization server by:
  1. **JWKS URL** (preferred), or
  2. **JWKS directly** at registration time.
- The auth server is expected to validate `private_key_jwt` using:
  - `kid`
  - optionally `jku`
  - the registration-time JWKS URL or JWKS value
- The profile requires support for asymmetric signatures including **`RS384`** or **`ES384`**.

Conclusion from the spec:

- **A manual registration step is absolutely allowed.**
- The real question is **whether the target authorization server actually implements SMART's JWKS-based validation behavior** after registration.

## Entra behavior investigated

### Observed Entra model

For app authentication, Microsoft Entra expects:

- client key material to be pre-registered on the app registration,
- commonly as an uploaded **X.509 certificate** (`keyCredentials`),
- and then uses that registered certificate identity to validate the client assertion.

Observed/confirmed constraints:

- Entra does **not** natively dereference a client-controlled **`jku` / JWKS URI** at token time.
- Entra's documented certificate-credential flow differs from SMART's asymmetric client profile.
- A matching **public key in raw JWKS form** is not treated as equivalent to a pre-registered Entra certificate identity.

## Experiments run

## 1. Proof kit creation

A proof kit was added under:

`samples/patientandpopulationservices-smartonfhir-oncg10-smart-v1/scripts/backend-direct-auth-proof/`

Artifacts added:

- `Test-BackendDirectAuth.ps1`
- `direct-backend-auth.http`
- `direct-backend-auth-entra.http`
- `Prepare-InfernoJwksUploadArtifacts.py`

Purpose:

- test direct backend token acquisition against standard Entra and Entra External,
- inspect resulting token claims,
- test direct FHIR access,
- and experiment with uploading Inferno-derived key material into Entra.

## 2. Inferno JWKS inspection

Inferno's SMART STU2 JWKS contains:

- one **EC / ES384** key
- one **RSA / RS384** key

The RSA key used for the upload experiment had:

- `kid`: `b41528b6f37a9500edb8a905a595bdd7`
- `alg`: `RS384`

## 3. JWKS-to-certificate conversion

We converted Inferno's **RSA JWK** (`n`, `e`) into:

- a PEM public key
- an X.509 certificate wrapping that public key
- a `.cer` artifact suitable for Entra upload

This proves that **Inferno's bare RSA public key can be transformed into an uploadable Entra artifact**.

## 4. Entra app registration upload test

Target tenant:

- `837cfeb2-55a3-4d9e-a40f-f822b4cd2391`

Target app registration:

- app/client ID: `d5fdde5d-5825-4276-840f-cd34d5966dd7`
- display name: `mikaelw-backend-test1`

Upload result:

- upload command succeeded
- Entra app object showed **1 keyCredential**
- uploaded key credential:
  - `displayName`: `CN=Inferno JWKS Upload Test`
  - `type`: `AsymmetricX509Cert`
  - `usage`: `Verify`
  - `customKeyIdentifier`: `EC6AF63255C3551E6EC6DEE6B75292C3B560AA89`

Conclusion:

- **Entra accepted the uploaded certificate artifact**
- so **manual registration into Entra is operationally possible**

## 5. Inferno runtime test against Entra

After uploading the Inferno-derived certificate, Inferno was run against Entra and the token request failed with:

```json
{
  "error": "invalid_client",
  "error_description": "AADSTS700027: The certificate with identifier used to sign the client assertion is not registered on application. [Reason - The key was not found.]"
}
```

Meaning:

- Entra did **not** accept the assertion as signed by a registered certificate
- even though the app had a certificate containing the same RSA public key material

## Interpretation of the failure

This failure strongly suggests that Entra is **not** validating Inferno's assertion as:

> "the signature verifies under a registered equivalent public key"

Instead, Entra appears to require something closer to:

> "the assertion references a specific certificate identity that matches registered Entra key credentials"

Why the wrapper-cert approach failed:

- Inferno publishes a **JWKS**
- Inferno does **not** publish the exact X.509 cert identity Entra expects
- we created a **new** certificate around the same public key
- that new certificate has a **different certificate identity / thumbprint**
- Entra still rejected the assertion

## What this proves

### Proven

- SMART allows manual client public-key registration.
- Entra can store manually uploaded certificate-based public-key material.
- A JWKS RSA public key can be transformed into an Entra-uploadable cert artifact.
- Uploading a cert derived from Inferno's RSA JWKS is **not sufficient** for Entra to accept Inferno's actual assertion.

### Not proven

- That standard Entra can satisfy Inferno's exact asymmetric client-authentication requirements
- That Entra External can satisfy Inferno's exact asymmetric client-authentication requirements
- That Entra can function as a native SMART `client-confidential-asymmetric` authorization server for `jwks_uri` / `jku`-based client registration

## Current conclusion

At this point, the evidence supports the following conclusion:

> **Manual registration alone is not the blocker.**
>
> The blocker is that **SMART JWKS-based client trust** and **Entra's certificate-based client assertion validation** are not equivalent models.

So while Entra can accept manually uploaded cert material, that does **not** make it a drop-in implementation of SMART asymmetric client authentication as used by Inferno.

## What would be needed for further testing

One of the following would be needed to continue the experiment meaningfully:

1. **Inferno's actual certificate material**
   - so the exact cert identity used in the assertion could be registered in Entra
2. **A backend client we control**
   - where we control the private key and can generate an Entra-compatible assertion format
3. **A SMART-aware auth layer**
   - that validates `jwks_uri` / JWKS directly instead of relying on Entra to do that validation

## Recommended next step

If the goal is to support Inferno `(g)(10)` asymmetric client authentication exactly as specified, the safest conclusion from this investigation is:

- **Do not assume Entra alone can replace a SMART-aware asymmetric client validator.**

If the goal is instead to prove direct backend auth for a **vendor-controlled client**, continue with the proof kit using:

- a backend app registration you control,
- a certificate you control,
- a client assertion generated in the format Entra expects,
- and then test direct FHIR access separately from Inferno.

## Entra External ID addendum

We also considered whether **Microsoft Entra External ID** changes the conclusion.

### What Entra External ID likely helps with

- Azure FHIR can trust Entra External ID as an **external OIDC identity provider**
- Entra External supports **machine-to-machine** patterns for app-only access
- Entra External also supports **federated credentials** for token exchange from trusted external issuers

### What Entra External ID likely does not change

The main mismatch for Inferno appears to remain the same:

- Inferno uses the SMART asymmetric client model:
  - registration by `jwks_uri` / JWKS
  - `private_key_jwt`
  - `RS384` / `ES384`
- Entra External still uses Microsoft identity platform authentication patterns:
  - pre-registered cert/key material
  - Entra-style client assertion validation, or
  - federated credential token exchange

This means the likely weak point is still:

> **Inferno -> Entra External**

not:

> **Entra External -> FHIR**

### Current conclusion on Entra External

At this stage, Entra External should be viewed as:

- **likely viable on the FHIR trust side**, but
- **not yet shown to solve Inferno's native SMART asymmetric client-auth flow**

In other words:

> Entra External may help FHIR trust an external issuer, but it does not appear to add a SMART-native `jwks_uri` / `jku` / `RS384`-style client authentication model that would make Inferno work unchanged.

### Practical takeaway

If the goal is:

- **vendor-controlled backend auth** -> Entra External may still be worth testing with Entra-compatible cert auth or federated credentials
- **Inferno exact asymmetric SMART client auth** -> Entra External is still likely restricted in the same core way as standard Entra, unless a SMART-aware shim is introduced
