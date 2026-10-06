// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Strategies;

namespace SMARTCustomOperations.AzureAuth
{
    /// <summary>
    /// Serves a self-contained EHR launch initiator page used to drive the SMART EHR launch flow
    /// (e.g. Inferno g10 "EHR Practitioner App" test). The page runs MSAL.js in the browser to
    /// sign in the practitioner, posts the launch context ({ userId = oid, launch }) to
    /// /api/context-cache, then redirects the browser to the SMART app's launch URL with the
    /// required iss and launch parameters.
    ///
    /// Entra IdP only; External mode returns 404 (external IdPs issue SMART launch context natively).
    /// </summary>
    public class EhrLaunchFunction
    {
        private readonly ILogger<EhrLaunchFunction> _logger;
        private readonly AzureAuthOperationsConfig _config;
        private readonly IIdpStrategy _idpStrategy;

        public EhrLaunchFunction(ILogger<EhrLaunchFunction> logger, AzureAuthOperationsConfig config, IIdpStrategy idpStrategy)
        {
            _logger = logger;
            _config = config;
            _idpStrategy = idpStrategy;
        }

        [Function("EhrLaunch")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/ehr-launch")] HttpRequestData req)
        {
            if (!_idpStrategy.ProvidesAuthorizeProxy)
            {
                var nf = req.CreateResponse(HttpStatusCode.NotFound);
                nf.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                await nf.WriteStringAsync("EHR launch initiator is not enabled in this mode.");
                return nf;
            }

            if (string.IsNullOrWhiteSpace(_config.TenantId) || string.IsNullOrWhiteSpace(_config.ContextAppClientId))
            {
                _logger.LogError("EhrLaunch requires TenantId and ContextAppClientId configuration.");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                err.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                await err.WriteStringAsync("EHR launch initiator is not configured. Set AZURE_TenantId and AZURE_ContextAppClientId.");
                return err;
            }

            var html = BuildHtml(_config.TenantId!, _config.ContextAppClientId!);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            response.Headers.Add("Cache-Control", "no-store");
            await response.WriteStringAsync(html);
            return response;
        }

        // JsonConvert.SerializeObject is used to JS-escape any injected string safely.
        private static string BuildHtml(string tenantId, string contextAppClientId)
        {
            var tenantIdJs = JsonConvert.SerializeObject(tenantId);
            var contextAppClientIdJs = JsonConvert.SerializeObject(contextAppClientId);

            return $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<title>EHR Launch Initiator</title>
<meta name="viewport" content="width=device-width,initial-scale=1" />
<style>
  * { box-sizing: border-box; }
  body { font-family: -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif; margin: 0; background: #faf9f8; color: #201f1e; }
  main { max-width: 640px; margin: 32px auto; padding: 24px 32px; background: #fff; border: 1px solid #edebe9; border-radius: 4px; }
  h1 { font-size: 22px; font-weight: 600; margin: 0 0 6px; }
  p.sub { color: #57606a; font-size: 13px; margin: 0 0 20px; }
  label { display: block; font-size: 13px; font-weight: 600; margin: 14px 0 4px; }
  input { width: 100%; font: inherit; padding: 8px 10px; border: 1px solid #8a8886; border-radius: 2px; }
  .hint { color: #6e7781; font-size: 12px; margin-top: 3px; }
  .actions { margin-top: 24px; }
  button { font: inherit; padding: 9px 22px; border-radius: 2px; border: 1px solid #0f6cbd; background: #0f6cbd; color: #fff; cursor: pointer; }
  button:hover { background: #115ea3; }
  button:disabled { background: #a6a6a6; border-color: #a6a6a6; cursor: default; }
  .status { padding: 10px 12px; border-radius: 2px; margin: 16px 0 0; font-size: 14px; }
  .status.info { background: #eff6fc; border: 1px solid #b3d7f2; }
  .status.error { background: #fde7e9; border: 1px solid #f1707b; }
  .status.progress { display: flex; align-items: center; gap: 10px; }
  .spinner { width: 16px; height: 16px; border: 2px solid #b3d7f2; border-top-color: #0f6cbd; border-radius: 50%; animation: spin 0.7s linear infinite; flex: 0 0 auto; }
  @keyframes spin { to { transform: rotate(360deg); } }
  .status.hidden { display: none; }
  .hidden { display: none; }
</style>
</head>
<body>
<main>
  <h1>EHR Launch Initiator</h1>
  <p class="sub">Simulates an EHR launching a SMART app. Signs in the practitioner, delivers launch
    context to the gateway, then redirects to the SMART app's launch URL.</p>

  <div id="form-block">
    <label for="launch-url">SMART app launch URL</label>
    <input id="launch-url" type="url" placeholder="https://inferno.healthit.gov/suites/custom/smart/launch" />
    <div class="hint">The app's launch URL (Inferno shows this when the EHR Practitioner test is waiting).</div>

    <label for="patient-id">Patient ID</label>
    <input id="patient-id" type="text" placeholder="PatientA" />
    <div class="hint">FHIR Patient logical id to place in launch context.</div>

    <label for="encounter-id">Encounter ID (optional)</label>
    <input id="encounter-id" type="text" placeholder="EncounterA" />

    <div class="actions">
      <button id="launch-btn" type="button">Sign in &amp; Launch</button>
    </div>
  </div>

  <div id="status" class="status hidden"></div>
</main>

<script src="https://cdn.jsdelivr.net/npm/@azure/msal-browser@3/lib/msal-browser.min.js"></script>
<script>
(function () {
  const TENANT_ID = {{tenantIdJs}};
  const CLIENT_ID = {{contextAppClientIdJs}};

  const statusEl = document.getElementById('status');
  const launchBtn = document.getElementById('launch-btn');
  const formBlock = document.getElementById('form-block');

  function setStatus(kind, text) {
    statusEl.className = 'status ' + kind;
    statusEl.textContent = text;
  }

  // Progress state: hide the form, scroll to top, and show a spinner next to the message so the
  // user sees work is happening instead of the empty form reappearing after submit / redirect.
  function setProgress(text) {
    formBlock.classList.add('hidden');
    window.scrollTo({ top: 0, behavior: 'smooth' });
    statusEl.className = 'status info progress';
    statusEl.innerHTML = '<span class="spinner"></span><span></span>';
    statusEl.lastChild.textContent = text;
  }

  function showFormError(text) {
    formBlock.classList.remove('hidden');
    launchBtn.disabled = false;
    setStatus('error', text);
  }

  // Persist the form across the MSAL sign-in redirect round-trip.
  const PENDING_KEY = 'ehr_launch_pending';

  const msalConfig = {
    auth: {
      clientId: CLIENT_ID,
      authority: `https://login.microsoftonline.com/${TENANT_ID}`,
      redirectUri: `${window.location.origin}/api/ehr-launch`,
    },
    cache: { cacheLocation: 'sessionStorage', storeAuthStateInCookie: false },
  };
  // Request an id_token (aud = ContextAppClientId) which the gateway's context-cache endpoint
  // accepts. Force a fresh sign-in so the practitioner identity is explicit.
  const loginRequest = { scopes: ['openid', 'profile'], prompt: 'login' };
  const msalInstance = new msal.PublicClientApplication(msalConfig);

  function base64UrlToJsonSafe(obj) {
    // Standard base64 (not URL-safe) to match the gateway's Base64 decode of the launch payload.
    return btoa(JSON.stringify(obj));
  }

  async function postContextAndRedirect(idToken, oid) {
    const pendingRaw = sessionStorage.getItem(PENDING_KEY);
    if (!pendingRaw) { return; }
    const pending = JSON.parse(pendingRaw);

    const launchProps = { patient: pending.patient };
    if (pending.encounter) { launchProps.encounter = pending.encounter; }
    const launchToken = base64UrlToJsonSafe(launchProps);

    setProgress('Delivering launch context to the gateway…');

    const resp = await fetch(`${window.location.origin}/api/context-cache`, {
      method: 'POST',
      headers: { 'Authorization': 'Bearer ' + idToken, 'Content-Type': 'application/json' },
      body: JSON.stringify({ userId: oid, launch: launchToken }),
    });

    if (!resp.ok && resp.status !== 204) {
      let detail = resp.statusText;
      try { detail = JSON.stringify(await resp.json()); } catch (e) { /* ignore */ }
      throw new Error(`context-cache returned ${resp.status}: ${detail}`);
    }

    sessionStorage.removeItem(PENDING_KEY);

    // Redirect the browser to the SMART app's launch URL with the required EHR launch params.
    const iss = window.location.origin;
    const url = new URL(pending.launchUrl);
    url.searchParams.set('iss', iss);
    url.searchParams.set('launch', launchToken);

    setProgress('Redirecting to the SMART app launch URL…');
    window.location.assign(url.toString());
  }

  function oidFromAccount(account) {
    return (account && (account.idTokenClaims && account.idTokenClaims.oid)) || (account && account.localAccountId) || '';
  }

  async function run() {
    await msalInstance.initialize();

    // Complete a sign-in redirect if we are returning from one.
    let result = null;
    try { result = await msalInstance.handleRedirectPromise(); } catch (e) {
      showFormError('Sign-in failed: ' + e.message);
      return;
    }

    if (result && result.account) {
      setProgress('Signing you in…');
      try {
        await postContextAndRedirect(result.idToken, oidFromAccount(result.account));
      } catch (e) {
        showFormError(e.message);
      }
      return;
    }

    launchBtn.addEventListener('click', async () => {
      const launchUrl = document.getElementById('launch-url').value.trim();
      const patient = document.getElementById('patient-id').value.trim();
      const encounter = document.getElementById('encounter-id').value.trim();

      if (!launchUrl) { setStatus('error', 'Enter the SMART app launch URL.'); return; }
      if (!patient) { setStatus('error', 'Enter a Patient ID.'); return; }
      try { new URL(launchUrl); } catch (e) { setStatus('error', 'Launch URL is not a valid URL.'); return; }

      sessionStorage.setItem(PENDING_KEY, JSON.stringify({ launchUrl, patient, encounter }));

      launchBtn.disabled = true;
      setProgress('Signing in…');

      // Try silent first, else interactive redirect.
      const accounts = msalInstance.getAllAccounts();
      if (accounts.length > 0) {
        try {
          const silent = await msalInstance.acquireTokenSilent({ scopes: ['openid', 'profile'], account: accounts[0] });
          await postContextAndRedirect(silent.idToken, oidFromAccount(silent.account));
          return;
        } catch (e) {
          /* fall through to interactive */
        }
      }
      await msalInstance.loginRedirect(loginRequest);
    });
  }

  run();
})();
</script>
</body>
</html>
""";
        }
    }
}
