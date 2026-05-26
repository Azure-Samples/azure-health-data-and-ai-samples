(function () {
    'use strict';

    /* ── FHIR result modal ─────────────────────────────────── */

    function openFhirModal() {
        document.getElementById('fhir-modal-title').textContent = (FHIR.resultType || 'FHIR') + ' Resource';

        var content;
        if (FHIR.resultJson) {
            try {
                content = JSON.stringify(JSON.parse(FHIR.resultJson), null, 2);
            } catch (e) {
                content = FHIR.resultJson;
            }
        } else {
            content = '(no data returned)';
        }

        document.getElementById('fhir-modal-content').textContent = content;
        document.getElementById('fhir-modal-overlay').style.display = 'block';
        document.body.style.overflow = 'hidden';
    }

    function closeFhirModal() {
        document.getElementById('fhir-modal-overlay').style.display = 'none';
        document.body.style.overflow = '';
    }

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') closeFhirModal();
    });

    var overlay = document.getElementById('fhir-modal-overlay');
    if (overlay) {
        overlay.addEventListener('click', function (e) {
            if (e.target === this) closeFhirModal();
        });
    }

    if (FHIR.autoOpen) {
        openFhirModal();
    }

    /* ── Resource fetch ─────────────────────────────────────── */

    function fetchResource() {
        var val = document.getElementById('resource-select').value;
        window.location.href = '/fhir?resource=' + encodeURIComponent(val);
    }

    function fetchBackendResource() {
        var el = document.getElementById('backend-resource-select');
        if (!el) return;
        var val = el.value;
        window.location.href = '/backend/fhir?resource=' + encodeURIComponent(val);
    }

    /* ── Scope panel logic ──────────────────────────────────── */

    var scopePanel          = document.getElementById('scope-panel');
    var scopeGroupIdentity  = document.getElementById('scope-group-identity');
    var scopeGroupLaunch    = document.getElementById('scope-group-launch');
    var scopeGroupPatient   = document.getElementById('scope-group-patient');
    var scopeGroupSystem    = document.getElementById('scope-group-system');
    var scopeLaunchPatient  = document.getElementById('scope-launch-patient');
    var scopeLaunchEhr      = document.getElementById('scope-launch-ehr');
    var scopeSummaryText    = document.getElementById('scope-summary-text');

    function configureScopePanel(type) {
        if (!scopePanel) return;

        if (type === 'backend' || type === 'ehr') {
            // Backend scope is fixed on the server (BackendServices:Scope).
            scopePanel.style.display          = 'none';
            scopeGroupIdentity.style.display  = 'none';
            scopeGroupLaunch.style.display    = 'none';
            scopeGroupPatient.style.display   = 'none';
            scopeGroupSystem.style.display    = 'none';
        } else {
            // Standalone (public or confidential): identity + launch/patient + patient scopes
            scopePanel.style.display          = 'block';
            scopeGroupIdentity.style.display  = 'block';
            scopeGroupLaunch.style.display    = 'block';
            scopeGroupPatient.style.display   = 'block';
            scopeGroupSystem.style.display    = 'none';
            if (scopeLaunchPatient) { scopeLaunchPatient.style.display = ''; scopeLaunchPatient.querySelector('input').checked = true; }
            if (scopeLaunchEhr)     { scopeLaunchEhr.style.display = 'none'; scopeLaunchEhr.querySelector('input').checked = false; }
        }

        updateScopeSummary();
    }

    function getSelectedScopes() {
        if (!scopePanel) return '';
        var checked = scopePanel.querySelectorAll('input[type="checkbox"]:checked');
        var scopes = [];
        for (var i = 0; i < checked.length; i++) {
            // Only include if parent scope-group is visible
            var group = checked[i].closest('.scope-group');
            if (group && group.style.display !== 'none') {
                // Only include if the individual label is visible
                var label = checked[i].closest('.scope-item');
                if (!label || label.style.display !== 'none') {
                    scopes.push(checked[i].value);
                }
            }
        }
        return scopes.join(' ');
    }

    function updateScopeSummary() {
        if (scopeSummaryText) {
            scopeSummaryText.textContent = getSelectedScopes() || '(none selected)';
        }
    }

    // Listen for checkbox changes to update the summary
    if (scopePanel) {
        scopePanel.addEventListener('change', function () {
            updateScopeSummary();
        });
    }

    /* ── Launch type selector ───────────────────────────────── */

    var launchOptions    = document.querySelectorAll('.launch-option');
    var backendInfo      = document.getElementById('backend-info');
    var confidentialInfo = document.getElementById('confidential-info');
    var ehrSimulatorPanel = document.getElementById('ehr-simulator');
    var backendTokenPanel = document.getElementById('backend-m2m-token');

    function applyLaunchType(type) {
        launchOptions.forEach(function (el) {
            var radio = el.querySelector('input[type="radio"]');
            el.classList.toggle('selected', radio && radio.value === type);
        });
        if (backendInfo)      backendInfo.style.display       = (type === 'backend')                 ? 'block' : 'none';
        if (confidentialInfo) confidentialInfo.style.display  = (type === 'standalone-confidential') ? 'block' : 'none';
        if (ehrSimulatorPanel) ehrSimulatorPanel.style.display = (type === 'ehr') ? 'block' : 'none';
        if (backendTokenPanel) backendTokenPanel.style.display = (type === 'backend') ? 'block' : 'none';

        configureScopePanel(type);
    }

    launchOptions.forEach(function (el) {
        el.addEventListener('click', function () {
            var radio = el.querySelector('input[type="radio"]');
            if (radio) {
                radio.checked = true;
                applyLaunchType(radio.value);
            }
        });
    });

    // Apply initial state on page load
    var checkedRadio = document.querySelector('.launch-option input[type="radio"]:checked');
    if (checkedRadio) {
        applyLaunchType(checkedRadio.value);
    } else if (typeof SELECTED_LAUNCH_TYPE !== 'undefined' && SELECTED_LAUNCH_TYPE) {
        applyLaunchType(SELECTED_LAUNCH_TYPE);
    }

    function startLaunch() {
        var radio = document.querySelector('.launch-option input[type="radio"]:checked');
        var type  = radio ? radio.value : 'standalone';

        if (type === 'backend') {
            var form = document.createElement('form');
            form.method = 'POST';
            form.action = '/backend/token';
            document.body.appendChild(form);
            form.submit();
            return;
        }

        var params = new URLSearchParams({ launchType: type });
        var scope = getSelectedScopes();
        if (scope) params.set('scope', scope);

        window.location.href = '/login?' + params.toString();
    }

    /* ── EHR Simulator scope panel ──────────────────────────── */

    var ehrScopePanel      = document.getElementById('ehr-scope-panel');
    var ehrScopeHidden     = document.getElementById('ehr-scope-hidden');
    var ehrScopeSummary    = document.getElementById('ehr-scope-summary-text');
    var ehrSimForm         = document.getElementById('ehr-sim-form');

    function getEhrSelectedScopes() {
        if (!ehrScopePanel) return '';
        var checked = ehrScopePanel.querySelectorAll('input[data-ehr-scope]:checked');
        var scopes = [];
        for (var i = 0; i < checked.length; i++) {
            scopes.push(checked[i].value);
        }
        return scopes.join(' ');
    }

    function updateEhrScopeSummary() {
        var s = getEhrSelectedScopes();
        if (ehrScopeSummary) ehrScopeSummary.textContent = s || '(none selected)';
        if (ehrScopeHidden)  ehrScopeHidden.value = s;
    }

    if (ehrScopePanel) {
        ehrScopePanel.addEventListener('change', updateEhrScopeSummary);
        updateEhrScopeSummary();
    }

    // Expose for inline event handlers
    window.closeFhirModal = closeFhirModal;
    window.fetchResource  = fetchResource;
    window.fetchBackendResource = fetchBackendResource;
    window.startLaunch    = startLaunch;

}());

