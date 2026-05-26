# SMART on FHIR — EHR Launch Design Notes

> **Audience:** humans and future Copilot sessions picking up this server.
> **Purpose:** Capture the current EHR-launch implementation, the test progress,
> open design questions, and how this server is expected to interop with a
> SMART **client app** that owns its own `/launch` endpoint.

---

## 1. Where we are (test matrix)

Running a 6-test matrix against `SMARTOnFhir.Server` on Entra ID.

| # | Scenario                                  | `UseConsentUI` | Status     |
|---|-------------------------------------------|----------------|------------|
| 1 | Standalone confidential                   | `false`        | ✅ passed  |
| 2 | Standalone PKCE/SPA                       | `false`        | ✅ passed  |
| 3 | **EHR launch**                            | `false`        | ⏸ pending |
| 4 | Standalone confidential                   | `true`         | ✅ passed  |
| 5 | Standalone PKCE/SPA                       | `true`         | ✅ passed  |
| 6 | **EHR launch**                            | `true`         | ⏸ pending |

Default config value today: `SmartConfig.UseConsentUI = false`.

---

## 2. Who owns `/launch`?

Per SMART App Launch v2, **`/launch` is a client-app endpoint, not an
authorization-server endpoint.** This server (`SMARTOnFhir.Server`) deliberately
does **not** expose `/launch`.

```
┌──────────┐  click app  ┌──────────┐  iss+launch ┌──────────────┐
│ Clinician│ ──────────▶ │   EHR    │ ──────────▶ │ Client app   │
│  in EHR  │             │ launcher │             │ /launch      │
└──────────┘             └──────────┘             └──────┬───────┘
                                                         │ discovers
                                                         │ smart-config
                                                         ▼
                                              ┌─────────────────────┐
                                              │ SMARTOnFhir.Server  │
                                              │ /auth/authorize     │
                                              │   ?launch=<token>   │
                                              └─────────────────────┘
```

So:
- **Client app `/launch`** does `iss` discovery (`GET {iss}/.well-known/smart-configuration`),
  then redirects the browser to **our** `/auth/authorize?launch=<token>&aud=...&scope=...`.
- **This server's `/auth/authorize`** resolves the opaque `launch` token to a
  context (`patient`, `encounter`, ...), correlates with Entra, and ultimately
  injects context into the token response.

---

## 3. Current server-side EHR-launch implementation

All three controllers cooperate; nothing the SMART app sees is correlated to
Entra directly.

### 3a. `Controllers/AuthorizeController.cs` — when `launch=` is present

1. Calls `DecodeLaunchContext(launch)` — today this **base64-decodes a JSON blob**
   `{ patient, encounter, need_patient_banner, ... }` (see "Open questions"
   below — this is the simulator shortcut).
2. Mints a server-only `flowId` (`Guid.NewGuid().ToString("N")`) and caches
   `EhrLaunchFlowState { LaunchContext, SmartRedirectUri, SmartState }` for 5 min.
3. Redirects to Entra with our own `redirect_uri = /auth/proxy-callback` and
   `state = flowId` — **the SMART app's redirect_uri and state never leave us.**

### 3b. `Controllers/ProxyCallbackController.cs` — when Entra redirects back

1. Looks up the in-flight flow by `state = flowId`.
2. Mints a `proxyCode` (`Guid.NewGuid().ToString("N")`) and caches
   `ProxyCodeEntry { EntraCode, ProxyCallbackUrl, LaunchContext }`.
3. Redirects to the **SMART app's original** `redirect_uri` with
   `code = proxyCode` and the SMART app's original `state`.

### 3c. `Controllers/TokenController.cs` — when SMART app posts the code

1. Detects the cached `proxyCode`, swaps it for Entra's real code, and resets
   `redirect_uri` to `/auth/proxy-callback` before forwarding to Entra.
2. After Entra returns tokens, injects the cached `LaunchContext` (patient,
   encounter, ...) into the response, plus `need_patient_banner=true` when any
   `launch*` scope is requested.

### 3d. Models in play

- `Models/SmartModels.cs::EhrLaunchFlowState`
- `Models/SmartModels.cs::ProxyCodeEntry`

### 3e. UI flag

`SmartConfig.UseConsentUI` only controls the **standalone** flow:
- `true`  → standalone requests carrying a launch scope are redirected to the
  React consent UI at `/auth/context/`, then re-issued with `user=true`.
- `false` → standalone requests go straight to Entra.

**EHR launches NEVER touch the React UI, regardless of `UseConsentUI`.** This is
by design.

---

## 4. Open design questions

### Q1 — Launch-token format: keep base64(JSON) or move to opaque?

| Option | Description | Effort |
|---|---|---|
| **A** | Keep base64(JSON). Document that real EHRs replace `DecodeLaunchContext` with a registry lookup. Most SMART sample servers do this. | none |
| **B** | Add `POST /ehr-sim/launch-token` taking `{patient, encounter}`, caches under random GUID for 5 min, returns the GUID. `AuthorizeController` switches to cache lookup. | ~30 lines + 1 small controller |

**Default recommendation:** A for shipping the sample, B if onboarding clarity
matters more than diff size. Either way the proxy-code/`/auth/proxy-callback`
machinery does not change.

### Q2 — `iss` validation

Today `/auth/authorize` does **not** check that the requesting client's
`iss` matches a registered EHR. Real EHR setups do. For a single-EHR sample
this is fine; flag it in production guidance.

---

## 5. Wiring to a sibling client app that owns `/launch`

If a SMART **client app** sits next to this server and exposes its own
`/launch` endpoint, the integration is just two facts:

1. **Client app's `/launch` discovers our config** at
   `{iss}/.well-known/smart-configuration` — which the
   `SmartConfigurationController` already overrides so it advertises
   `authorization_endpoint = https://<server>/auth/authorize` and
   `token_endpoint = https://<server>/auth/token`. ✅ no server change needed.

2. **Client app's `/launch` redirects** the browser to:

   ```
   {authorization_endpoint}
     ?response_type=code
     &client_id={client's Entra app id}
     &redirect_uri={client's callback}
     &scope=launch openid fhirUser offline_access patient/Patient.rs ...
     &state={client state}
     &aud={fhir audience}
     &launch={opaque launch token issued by the EHR / launcher}
   ```

   The server's `AuthorizeController` already handles this. ✅

What still needs to happen for end-to-end testing with the real client:

- [ ] **Decide Q1 (A or B).** If B, expose `/ehr-sim/launch-token` so the
      client (or a small launcher page) has a way to obtain a real opaque
      token instead of a hand-crafted base64 string.
- [ ] **Ensure client's redirect_uri is registered** on the client's Entra
      app registration (Web platform for confidential, SPA for public/PKCE).
- [ ] **`iss` value the client sends to discovery** must equal the value used
      by the auth server (typically the FHIR server base URL).
- [ ] **`aud` parameter** must equal `SmartConfig.FhirAudience` (or
      `FhirServerUrl`); both are accepted in `AuthorizeController`.

---

## 6. Next steps when work resumes

1. Pick Q1 option (A or B). If B, implement `/ehr-sim/launch-token` first.
2. Run **Test #3** — EHR launch with `UseConsentUI = false`.
3. Run **Test #6** — EHR launch with `UseConsentUI = true` (no UI expected
   because EHR launches bypass UI regardless).
4. Once both pass: wire to the sibling client app's `/launch` and repeat
   the end-to-end flow against the real client.

---

## 7. Files to read first when resuming

- [Controllers/AuthorizeController.cs](../Controllers/AuthorizeController.cs) — `HandleAuthorize`, `DecodeLaunchContext`
- [Controllers/ProxyCallbackController.cs](../Controllers/ProxyCallbackController.cs)
- [Controllers/TokenController.cs](../Controllers/TokenController.cs) — `HandleAuthorizationCode`, `ForwardToEntraAndEnrich`
- [Controllers/SmartConfigurationController.cs](../Controllers/SmartConfigurationController.cs) — endpoint rewriting
- [Configuration/SmartConfig.cs](../Configuration/SmartConfig.cs) — `UseConsentUI` semantics
- [Models/SmartModels.cs](../Models/SmartModels.cs) — `EhrLaunchFlowState`, `ProxyCodeEntry`
