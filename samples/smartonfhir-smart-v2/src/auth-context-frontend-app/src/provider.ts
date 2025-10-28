export enum IdpProvider {
    EntraID = "EntraID",
    EntraExternalID = "EntraExternalID",
    AzureADB2C = "AzureADB2C"
}

export const providerDomains: Record<IdpProvider, string> = {
    [IdpProvider.EntraID]: "",
    [IdpProvider.EntraExternalID]: ".ciamlogin.com",
    [IdpProvider.AzureADB2C]: ".b2clogin.com"
};