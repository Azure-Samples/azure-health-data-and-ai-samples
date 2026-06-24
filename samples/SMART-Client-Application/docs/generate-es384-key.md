# Generate the ES384 key pair

The Backend Services flow (`grant_type=client_credentials` + `private_key_jwt`) signs a JWT assertion with an **ECDSA P-384** private key. The IdP holds the matching public key in its JWKS and verifies the signature on every token request.

This page generates the key pair once, then shows how to install it for each IdP.

---

## 1. Generate the key pair

You need OpenSSL (Linux, macOS, Windows-WSL, or Git Bash on Windows).

```bash
# 1. Private key — keep this secret, never commit it
openssl ecparam -genkey -name secp384r1 -noout -out es384_private.pem

# 2. Public key — upload this to the IdP
openssl ec -in es384_private.pem -pubout -out es384_public.pem
```

You should now have two files:

```
es384_private.pem   ← stays on the machine running the client sample
es384_public.pem    ← uploaded to Okta (or converted to JWK for Entra)
```

---

## 2. Install the private key for the client sample

Copy `es384_private.pem` into the project's `keys/` folder:

```text
SMART-Client-Application/
  SMART-Native-Standalone-EHR-Launch/
    keys/
      es384_private.pem   ← here
```

The folder already has a `.gitignore` rule for `*.pem`, so the private key cannot be accidentally committed.

Then point [`appsettings.json`](../SMART-Native-Standalone-EHR-Launch/appsettings.json) at it:

```jsonc
{
  "BackendServices": {
    "PrivateKeyPath": "keys/es384_private.pem",
    "KeyId":          "<see per-IdP section below>"
  }
}
```

> [!NOTE]
> `BackendServices:PrivateKeyPath` is resolved relative to the project's content root, so you can also use an absolute path (e.g. for production secret stores).

---

## 3. Upload the public key — Okta

1. In the Okta Admin Console, open the **`smart-backend-services`** API Services app you registered in [idp-setup-okta.md](idp-setup-okta.md).
2. Go to **General → Client Credentials → Edit**.
3. Set **Client authentication** to **Public key / Private key**.
4. Click **Add key**, paste the contents of `es384_public.pem` or add jwks url where public key is published and save.
5. Copy the **Key ID (kid)** Okta displays for the new key — paste it into `BackendServices:KeyId` in [`appsettings.json`](../SMART-Native-Standalone-EHR-Launch/appsettings.json).

That's all — the proxy's SMART discovery will route the client sample's token request to the right Okta endpoint.

---

## 4. Upload the public key — Microsoft Entra ID

Entra fronts the proxy via the Azure Function App, and the proxy's Backend Services validator reads JWKS from a Key Vault secret tagged with `jwks_url`. So instead of uploading raw PEM, you publish a JWK to Key Vault.

### A. Create a JWK key

Pick any tool that can convert a PEM EC public key to a JWK — common options:

The key should look like:

```json
{
  "keys": [
    {
      "kty": "EC",
      "crv": "P-384",
      "x":   "<base64url>",
      "y":   "<base64url>",
      "kid": "smart-key-1",
      "alg": "ES384",
      "use": "sig"
    }
  ]
}
```

### B. Publish the JWKS via Key Vault

The proxy sample's deployment provisions a Key Vault and reads JWKS-URL hints from secrets that carry the `jwks_url` tag. Follow the proxy sample's instructions in `docs/ad-apps/smart-client-app-registrations.md` (Backend Services section) to:

1. Create a **secret** in the proxy's Key Vault whose **value** is the JWKS JSON above.
2. Set a **tag** named `jwks_url` whose value the proxy will substitute when validating client assertions for the Backend Services client. The exact tag value pattern is documented in that page.
3. Set `BackendServices:KeyId` in [`appsettings.json`](../SMART-Native-Standalone-EHR-Launch/appsettings.json) to the same `kid` you chose for the JWK (e.g. `smart-key-1`).

> [!IMPORTANT]
> The `kid` you pick **must** match exactly between:
> - the JWK in your Key Vault secret, and
> - `BackendServices:KeyId` in `appsettings.json`.
> The client's signed assertion includes that `kid` in its JWS header, and the proxy uses it to pick the right key from the JWKS.

---

## 5. Quick sanity check

Run the client sample, click **Backend Services → Launch**, and inspect the response on the dashboard:

- ✓ `access_token` returned, `token_type: "Bearer"`, `scope: "system/*.rs"` → success.
- ✗ `invalid_client` / `invalid_request` → either the `kid` doesn't match, the public key was uploaded incorrectly, or the private key on disk doesn't match the public key in the IdP. Re-run step 1 to make sure the two PEMs are a pair.
- ✗ `invalid_grant: client_assertion is invalid` → check that `aud` of the assertion equals the IdP's token endpoint. The sample sets this automatically from SMART discovery; if you're seeing this, the discovery URL is probably wrong (see [troubleshooting.md](troubleshooting.md)).

For the full M2M walkthrough, see [flows.md → Backend Services](flows.md#backend-services-m2m).

---

[Back to the main README](../README.md) · [Configuration reference](configuration.md)
