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
    /// Serves the self-contained HTML picker page. Entra IdP only; External mode returns 404.
    /// The page runs MSAL.js in the browser to authenticate the user, then calls
    /// /api/appConsentInfo (GET/POST) to read and narrow the user's oauth2PermissionGrant.
    /// </summary>
    public class ConsentUiFunction
    {
        private readonly ILogger<ConsentUiFunction> _logger;
        private readonly AzureAuthOperationsConfig _config;
        private readonly IIdpStrategy _idpStrategy;

        public ConsentUiFunction(ILogger<ConsentUiFunction> logger, AzureAuthOperationsConfig config, IIdpStrategy idpStrategy)
        {
            _logger = logger;
            _config = config;
            _idpStrategy = idpStrategy;
        }

        [Function("ConsentUi")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/consent-ui")] HttpRequestData req)
        {
            if (!_idpStrategy.ProvidesConsentPicker)
            {
                var nf = req.CreateResponse(HttpStatusCode.NotFound);
                nf.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                await nf.WriteStringAsync("Consent picker is not enabled in this mode.");
                return nf;
            }

            if (string.IsNullOrWhiteSpace(_config.TenantId) || string.IsNullOrWhiteSpace(_config.ContextAppClientId))
            {
                _logger.LogError("ConsentUi requires TenantId and ContextAppClientId configuration.");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                err.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                await err.WriteStringAsync("Consent picker is not configured. Set AZURE_TenantId and AZURE_ContextAppClientId.");
                return err;
            }

            var contextAppClientId = _config.ContextAppClientId!;

            // Default: request only OIDC scopes. MSAL returns an id_token whose aud=ContextAppClientId,
            // which ContextTokenValidator accepts. This avoids needing an Application ID URI or an
            // exposed API scope on the context app. Override with ConsentPickerAudience if you want
            // a bespoke resource-audience token (must be listed as a valid audience by the validator).
            var pickerScope = string.IsNullOrWhiteSpace(_config.ConsentPickerAudience)
                ? "openid profile"
                : _config.ConsentPickerAudience!;

            var html = BuildHtml(
                tenantId: _config.TenantId!,
                contextAppClientId: contextAppClientId,
                pickerScope: pickerScope);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            response.Headers.Add("Cache-Control", "no-store");
            await response.WriteStringAsync(html);
            return response;
        }

        // JsonConvert.SerializeObject is used to JS-escape any injected string safely.
        private static string BuildHtml(string tenantId, string contextAppClientId, string pickerScope)
        {
            var tenantIdJs = JsonConvert.SerializeObject(tenantId);
            var contextAppClientIdJs = JsonConvert.SerializeObject(contextAppClientId);
            var pickerScopeJs = JsonConvert.SerializeObject(pickerScope);

            return $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<title>Consent picker</title>
<meta name="viewport" content="width=device-width,initial-scale=1" />
<style>
  * { box-sizing: border-box; }
  body { font-family: -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif; margin: 0; background: #fff; color: #201f1e; }
  main { max-width: 820px; margin: 24px auto; padding: 0 32px; }
  h1 { font-size: 24px; font-weight: 600; margin: 0 0 20px; }
  h3 { font-size: 16px; font-weight: 700; margin: 22px 0 8px; }
  .welcome { font-size: 20px; font-weight: 600; margin: 0 0 2px; }
  .email { color: #444; font-size: 13px; }
  .review-note { font-size: 13px; margin: 4px 0; }
  a.link { color: #0f6cbd; text-decoration: none; cursor: pointer; font-size: 13px; }
  a.link:hover { text-decoration: underline; }
  .requesting { font-size: 18px; font-weight: 600; margin: 20px 0 4px; }
  ul.scopes { list-style: none; padding: 0; margin: 0; }
  ul.scopes li { padding: 3px 0; font-size: 14px; }
  ul.scopes.plain li .name { color: #0f6cbd; }
  ul.scopes.select li { display: flex; align-items: center; gap: 8px; }
  ul.scopes.select li input { width: 16px; height: 16px; margin: 0; }
  ul.scopes.select li .name { color: #0f6cbd; }
  ul.scopes.select li input:disabled ~ .meta { opacity: 0.55; }
  ul.scopes li .meta { flex: 1; }
  ul.scopes li .desc { color: #57606a; font-size: 12px; }
  .empty { color: #6e7781; font-style: italic; padding: 4px 0; }
  .actions { margin-top: 24px; display: flex; gap: 10px; }
  button { font: inherit; padding: 8px 20px; border-radius: 2px; border: 1px solid #8a8886; background: #fff; color: #201f1e; cursor: pointer; }
  button.primary { background: #0f6cbd; color: #fff; border-color: #0f6cbd; }
  button.primary:hover { background: #115ea3; }
  button.primary:disabled { background: #a6a6a6; border-color: #a6a6a6; cursor: default; }
  .status { padding: 10px 12px; border-radius: 2px; margin: 12px 0; font-size: 14px; }
  .status.info { background: #eff6fc; border: 1px solid #b3d7f2; }
  .status.error { background: #fde7e9; border: 1px solid #f1707b; }
  .status.ok { background: #dff6dd; border: 1px solid #6fdd8b; }
  .hidden { display: none; }
</style>
</head>
<body>
<main>
  <h1>Sample Auth Context Frontend App</h1>
  <div id="status" class="status info">Signing you in…</div>
  <div id="app-block" class="hidden">
    <div class="welcome" id="welcome"></div>
    <div class="email" id="user-email"></div>
    <div class="review-note">Please review the below access permissions before continuing.</div>
    <a class="link" id="logout-link">Logout</a>

    <div class="requesting" id="requesting"></div>

    <h3>Requested Access:</h3>
    <ul class="scopes plain" id="requested-list"></ul>
    <div id="requested-empty" class="empty hidden">No scopes requested.</div>

    <div id="review-mode" class="hidden">
      <h3>Approved Access:</h3>
      <ul class="scopes plain" id="approved-list"></ul>
      <div id="approved-empty" class="empty hidden">No previously approved scopes.</div>
      <div class="actions">
        <button type="button" id="continue-btn" class="primary">Continue</button>
        <button type="button" id="change-btn">Change Access</button>
      </div>
    </div>

    <div id="edit-mode" class="hidden">
      <h3>Select Access:</h3>
      <ul class="scopes select" id="select-list"></ul>
      <div class="actions">
        <button type="button" id="update-btn" class="primary">Continue</button>
      </div>
    </div>
  </div>
</main>

<script src="https://cdn.jsdelivr.net/npm/@azure/msal-browser@3/lib/msal-browser.min.js"></script>
<script>
(function () {
  const TENANT_ID = {{tenantIdJs}};
  const CLIENT_ID = {{contextAppClientIdJs}};
  const PICKER_SCOPE = {{pickerScopeJs}};

  const query = new URLSearchParams(window.location.search);
  const smartClientId = query.get('client_id') || '';
  const requestedScopeParam = query.get('scope') || '';
  const requestedScopes = requestedScopeParam
    .split(/[+\s]+/)
    .map(s => s.trim().replace(/\//g, '.'))
    .filter(Boolean);

  const sleep = ms => new Promise(r => setTimeout(r, ms));

  // Technical scopes that are always granted but never shown or de-selectable in the picker.
  // They must still be forwarded to the IdP for the token / launch context (fhirUser, openid,
  // launch, launch/patient, ...). Matches the reference sample's hide-and-always-enable rule.
  function shouldHideScope(name) {
    const n = (name || '').toLowerCase();
    return n.includes('launch') || n === 'openid' || n === 'fhiruser';
  }

  // Scopes shown to the user in "Requested Access" — original SMART form (slashes), minus the
  // always-granted technical scopes above.
  const requestedScopesDisplay = requestedScopeParam
    .split(/[+\s]+/)
    .map(s => s.trim())
    .filter(Boolean)
    .filter(s => !shouldHideScope(s));

  // Build the /api/authorize URL to launch the SMART flow with a chosen scope. All original
  // SMART launch params (redirect_uri, state, aud, code_challenge, launch, etc.) are forwarded;
  // scope is replaced with the selected list; prompt=consent is added so Entra reflects the
  // narrowed grant on the next token.
  function buildAuthorizeUrl(selectedScopes) {
    const params = new URLSearchParams(window.location.search);
    params.set('scope', selectedScopes.join(' '));
    params.set('prompt', 'consent');
    // Marker so /api/authorize continues to the IdP instead of looping back to the picker.
    params.set('user', 'true');
    return '/api/authorize?' + params.toString();
  }

  const statusEl = document.getElementById('status');
  const appBlock = document.getElementById('app-block');
  const reviewMode = document.getElementById('review-mode');
  const editMode = document.getElementById('edit-mode');
  const changeBtn = document.getElementById('change-btn');
  const continueBtn = document.getElementById('continue-btn');
  const updateBtn = document.getElementById('update-btn');
  const logoutLink = document.getElementById('logout-link');

  function setStatus(kind, text) {
    statusEl.className = 'status ' + kind;
    statusEl.textContent = text;
    statusEl.classList.remove('hidden');
  }
  function hideStatus() { statusEl.classList.add('hidden'); }

  const msalConfig = {
    auth: {
      clientId: CLIENT_ID,
      authority: `https://login.microsoftonline.com/${TENANT_ID}`,
      redirectUri: `${window.location.origin}/api/consent-ui`,
    },
    cache: { cacheLocation: 'sessionStorage', storeAuthStateInCookie: false },
  };
  // Match reference sample: force a fresh sign-in every time the picker page loads.
  const tokenRequest = { scopes: PICKER_SCOPE.split(/\s+/).filter(Boolean), prompt: 'login' };
  const msalInstance = new msal.PublicClientApplication(msalConfig);

  async function ensureToken() {
    await msalInstance.initialize();
    const redirectResult = await msalInstance.handleRedirectPromise();
    let account = redirectResult ? redirectResult.account : msalInstance.getAllAccounts()[0];
    if (!account) {
      await msalInstance.loginRedirect(tokenRequest);
      return null;
    }
    msalInstance.setActiveAccount(account);
    activeAccount = account;
    try {
      const r = await msalInstance.acquireTokenSilent({ ...tokenRequest, account });
      // The server-side ContextTokenValidator accepts tokens whose aud equals the ContextApp
      // client id. That's the id_token by default; access_token only carries that aud when the
      // caller has configured a resource audience via ConsentPickerAudience.
      return r.idToken || r.accessToken;
    } catch (err) {
      if (err && err.name === 'InteractionRequiredAuthError') {
        await msalInstance.acquireTokenRedirect(tokenRequest);
        return null;
      }
      throw err;
    }
  }

  async function fetchConsent(token) {
    if (!smartClientId || !requestedScopeParam) {
      throw new Error('Missing required query parameters client_id and scope.');
    }
    const url = `/api/appConsentInfo?client_id=${encodeURIComponent(smartClientId)}&scope=${encodeURIComponent(requestedScopeParam)}`;
    const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
    if (!res.ok) {
      throw new Error(`GET /api/appConsentInfo returned ${res.status}: ${await res.text()}`);
    }
    return res.json();
  }

  function classify(scope, requestedSet) {
    const inRequested = requestedSet.has(scope.name);
    if (inRequested && !scope.consented) return 'requested';
    if (scope.consented && !inRequested) return 'granted';
    if (inRequested && scope.consented) return 'granted'; // already have it
    return 'granular';
  }

  // Convert %2f back to '/' for display (SMART form is friendlier for a reviewer to read).
  function displayName(n) { return (n || '').replace(/%2f/gi, '/'); }

  // Renders a read-only list of scope names.
  function renderPlain(listId, emptyId, names) {
    const ul = document.getElementById(listId);
    const empty = document.getElementById(emptyId);
    ul.innerHTML = '';
    if (!names.length) {
      empty && empty.classList.remove('hidden');
      return;
    }
    empty && empty.classList.add('hidden');
    for (const n of names) {
      const li = document.createElement('li');
      const meta = document.createElement('div');
      meta.className = 'meta';
      const name = document.createElement('div');
      name.className = 'name';
      name.textContent = displayName(n);
      meta.appendChild(name);
      li.appendChild(meta);
      ul.appendChild(li);
    }
  }

  // A scope is a "parent" of another if the other's name STARTS WITH the parent name and
  // is different (e.g. patient.Observation.rs vs patient.Observation.rs?category=...).
  function isParentOf(parentName, childName) {
    return childName !== parentName && childName.startsWith(parentName);
  }

  // Renders the editable list. Each row has a checkbox reflecting `s.enabled`. A row is
  // disabled in UI when any other checked scope is a prefix of this scope's name — the
  // user picks either the broad scope or the granular variants, never both.
  function renderEditable(listId, scopes) {
    const ul = document.getElementById(listId);
    ul.innerHTML = '';
    for (const s of scopes) {
      // Hidden technical scopes (launch*, openid, fhirUser) are never shown; they stay enabled.
      if (s.hidden) continue;
      const li = document.createElement('li');
      const cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.dataset.name = s.name;
      cb.checked = !!s.enabled;
      cb.disabled = scopes.some(o => o.enabled && isParentOf(o.name, s.name));
      cb.addEventListener('change', () => {
        s.enabled = cb.checked;
        if (cb.checked) {
          // Enabling a parent clears its children.
          for (const child of scopes) {
            if (isParentOf(s.name, child.name)) child.enabled = false;
          }
        }
        renderEditable(listId, scopes);
      });

      const meta = document.createElement('div');
      meta.className = 'meta';
      const name = document.createElement('div');
      name.className = 'name';
      name.textContent = displayName(s.name);
      const desc = document.createElement('div');
      desc.className = 'desc';
      desc.textContent = s.userDescription || '';
      meta.appendChild(name);
      if (desc.textContent) meta.appendChild(desc);
      li.appendChild(cb);
      li.appendChild(meta);
      ul.appendChild(li);
    }
  }

  let currentInfo = null;
  let currentToken = null;
  let activeAccount = null;

  function render(info) {
    const appName = info.applicationName || info.applicationId || 'this application';
    document.getElementById('requesting').textContent =
      'Application ' + appName + ' is requesting access to your information.';

    if (activeAccount) {
      document.getElementById('welcome').textContent = 'Welcome ' + (activeAccount.name || 'User') + '!';
      document.getElementById('user-email').textContent = activeAccount.username || '';
    }

    // Mark technical scopes (launch*, openid, fhirUser) as hidden and ALWAYS enabled. They are
    // never shown or de-selectable, but must still flow to the IdP for the token/launch context.
    // Every other scope seeds its `enabled` flag from the current `consented` grant.
    for (const s of info.scopes) {
      s.hidden = shouldHideScope(s.name);
      s.enabled = s.hidden ? true : !!s.consented;
    }

    renderPlain('requested-list', 'requested-empty', requestedScopesDisplay);

    // Approved Access excludes the always-granted hidden scopes (matches reference sample).
    const consentedVisible = info.scopes.filter(s => s.consented && !s.hidden).map(s => s.name);
    renderPlain('approved-list', 'approved-empty', consentedVisible);

    hideStatus();
    appBlock.classList.remove('hidden');
    currentInfo = info;

    // If the user has any prior (visible) consented scopes, start in review mode; else edit.
    if (consentedVisible.length > 0) {
      showReviewMode();
    } else {
      showEditMode();
    }
  }

  function showReviewMode() {
    reviewMode.classList.remove('hidden');
    editMode.classList.add('hidden');
  }

  function showEditMode() {
    reviewMode.classList.add('hidden');
    editMode.classList.remove('hidden');
    renderEditable('select-list', currentInfo.scopes);
  }

  function collectSelection() {
    for (const s of currentInfo.scopes) s.consented = !!s.enabled;
    return currentInfo;
  }

  async function submit() {
    const body = collectSelection();
    updateBtn.disabled = true;
    // Same message the reference sample shows while it waits for Graph to replicate the change.
    setStatus('info', 'Saving your preferences...this may take a bit...');
    const res = await fetch('/api/appConsentInfo', {
      method: 'POST',
      headers: { Authorization: `Bearer ${currentToken}`, 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    if (!res.ok) {
      updateBtn.disabled = false;
      throw new Error(`POST /api/appConsentInfo returned ${res.status}: ${await res.text()}`);
    }

    // Wait for Microsoft Entra / Graph to reflect the narrowed grant before redirecting to
    // /authorize. Without this, Entra may still show the previous (broader) consent because the
    // oauth2PermissionGrant change has not replicated yet. Poll until the current consented count
    // drops to what the user selected, then wait a bit longer for replication. (Matches reference.)
    const userInputConsentedScopeCount = body.scopes.filter(s => s.consented).length;
    let scopeSaveSuccessful = false;
    for (let attempt = 0; attempt < 10; attempt++) {
      const newInfo = await fetchConsent(currentToken);
      const currentGraphConsentCount = newInfo.scopes.filter(s => s.consented).length;
      console.log(`Checking if scopes have been removed. Attempt ${attempt} of 10. User consented: ${userInputConsentedScopeCount}. Graph consented: ${currentGraphConsentCount}.`);
      if (userInputConsentedScopeCount >= currentGraphConsentCount) {
        scopeSaveSuccessful = true;
        break;
      }
      await sleep(1000 * (attempt + 1));
    }

    // Give Graph more time to replicate the consent information.
    console.log('Sleeping for 5 seconds to ensure Graph has the latest consent information.');
    await sleep(5000);

    if (!scopeSaveSuccessful) {
      updateBtn.disabled = false;
      throw new Error('Scopes did not properly replicate. Please try again.');
    }

    // Forward the visible selected scopes PLUS the always-granted hidden scopes (launch*, openid,
    // fhirUser) taken from the original request — the latter are needed for the token/launch
    // context even though they were never shown.
    const selectedVisible = body.scopes.filter(s => s.consented && !s.hidden).map(s => s.name);
    const hiddenRequested = requestedScopeParam.split(/[+\s]+/).map(s => s.trim()).filter(Boolean).filter(shouldHideScope);
    const forward = Array.from(new Set([...selectedVisible, ...hiddenRequested]));
    window.location.href = buildAuthorizeUrl(forward);
  }

  logoutLink.addEventListener('click', () => msalInstance.logoutRedirect());
  changeBtn.addEventListener('click', () => showEditMode());
  continueBtn.addEventListener('click', () => {
    // "Continue" from review = proceed with the already-approved (visible) scopes plus the
    // always-granted hidden scopes (launch*, openid, fhirUser) from the original request.
    const approvedVisible = currentInfo.scopes.filter(s => s.consented && !s.hidden).map(s => s.name);
    const hiddenRequested = requestedScopeParam.split(/[+\s]+/).map(s => s.trim()).filter(Boolean).filter(shouldHideScope);
    const forward = Array.from(new Set([...approvedVisible, ...hiddenRequested]));
    window.location.href = buildAuthorizeUrl(forward);
  });
  updateBtn.addEventListener('click', async () => {
    try { await submit(); }
    catch (e) { setStatus('error', e.message || String(e)); }
  });

  (async () => {
    try {
      const token = await ensureToken();
      if (!token) return; // redirect in progress
      currentToken = token;
      setStatus('info', 'Loading consent information…');
      const info = await fetchConsent(token);
      render(info);
    } catch (e) {
      setStatus('error', e.message || String(e));
    }
  })();
})();
</script>
</body>
</html>
""";
        }
    }
}
