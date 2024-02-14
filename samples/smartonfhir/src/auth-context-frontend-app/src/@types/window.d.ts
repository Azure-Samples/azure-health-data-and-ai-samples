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
            REACT_APP_B2C_Tenant_Name: string;
            REACT_APP_SmartonFhir_with_B2C: bool;
            REACT_APP_B2C_Authority_URL: string;
        }
    }
}