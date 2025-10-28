export { };

declare global {
    interface Window {
        ENV_CONFIG: {
            REACT_APP_AAD_APP_CLIENT_ID: string;
            REACT_APP_AAD_APP_TenantId: string;
            REACT_APP_AAD_APP_REDIRECT_URI: string;
            REACT_APP_API_BASE_URL: string;
            REACT_APP_APPLICATIONINSIGHTS_CONNECTION_STRING: string;
            REACT_APP_FHIR_RESOURCE_AUDIENCE: string;
            REACT_APP_IDP_Provider_Tenant_Name: string;
            REACT_APP_IDP_Provider: string;
            REACT_APP_Authority_URL: string;
        }
    }
}