/*
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

import { LogLevel } from "@azure/msal-browser";

/**
 * Configuration object to be passed to MSAL instance on creation. 
 * For a full list of MSAL.js configuration parameters, visit:
 * https://github.com/AzureAD/microsoft-authentication-library-for-js/blob/dev/lib/msal-browser/docs/configuration.md 
 */
export const msalConfig = {
    auth: {
        clientId: window.ENV_CONFIG.REACT_APP_AAD_APP_CLIENT_ID,
        authority: window.ENV_CONFIG.SmartonFhir_with_B2C ? window.ENV_CONFIG.B2C_Authority_URL : `https://login.microsoftonline.com/${window.ENV_CONFIG.TenantId}`,
        redirectUri: window.location.protocol + "//" + window.location.host + window.location.pathname,
        knownAuthorities: [window.ENV_CONFIG.B2C_Tenant_Name + ".b2clogin.com"],  //Todo value will be null in smartonfhir so need to Check whether its failing in smartonfhir or not.
        postLogoutRedirectUri: "https://www.microsoft.com",
    },
    cache: {
        cacheLocation: "sessionStorage", // This configures where your cache will be stored
        storeAuthStateInCookie: false, // Set this to "true" if you are having issues on IE11 or Edge
    },
    system: {	
        loggerOptions: {	
            loggerCallback: (level: LogLevel, message : string, containsPii : boolean) => {	
                if (containsPii) {		
                    return;		
                }		
                switch (level) {
                    case LogLevel.Error:
                        console.error(message);
                        return;
                    case LogLevel.Info:
                        console.info(message);
                        return;
                    case LogLevel.Verbose:
                        console.debug(message);
                        return;
                    case LogLevel.Warning:
                        console.warn(message);
                        return;
                    default:
                        return;
                }	
            }	
        }	
    }
};

export const scopes: string[] = [`${window.ENV_CONFIG.REACT_APP_FHIR_RESOURCE_AUDIENCE}/user_impersonation`];
export const apiEndpoint: string = (window.location.host.includes("localhost") ? "http://localhost:7081/api" : `${window.ENV_CONFIG.REACT_APP_API_BASE_URL}/auth`) || "http://localhost:7071/api";
//export const apiEndpoint: string = window.ENV_CONFIG.REACT_APP_API_BASE_URL ?? "http://localhost:7071/api";