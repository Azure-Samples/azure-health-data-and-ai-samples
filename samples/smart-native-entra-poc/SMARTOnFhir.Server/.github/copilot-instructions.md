- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.

## Project context

- This is the SMART on FHIR v2 authorization server (ASP.NET 8) that fronts Entra ID for FHIR clients.
- **EHR-launch design, test-matrix state, open questions, and client-app wiring notes** live in
  [`docs/EHR-LAUNCH-DESIGN.md`](../docs/EHR-LAUNCH-DESIGN.md). Read it before changing
  `AuthorizeController`, `ProxyCallbackController`, `TokenController`, or anything that touches
  the `launch` parameter / `EhrLaunchFlowState` / `ProxyCodeEntry`.
- `/launch` is a **client-app** endpoint, not an auth-server endpoint. Do not add `/launch` here.
