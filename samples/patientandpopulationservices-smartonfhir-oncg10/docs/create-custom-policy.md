# Create Custom Policy

This document guides you through the steps needed to create Custom Policy in Azure B2C Tenant. This sample creates Signing and Encryption key along with two Application Registartion and a Custom User Flow using custom policies.

- Flow the below mentioned steps: 
    - [Create Signing and Encryption Key](https://learn.microsoft.com/en-us/azure/active-directory-b2c/tutorial-create-user-flows?pivots=b2c-custom-policy#add-signing-and-encryption-keys-for-identity-experience-framework-applications)
    - [Create Identity Experience Framework applications](https://learn.microsoft.com/en-us/azure/active-directory-b2c/tutorial-create-user-flows?pivots=b2c-custom-policy#register-identity-experience-framework-applications)


    - Update the custom policies provided at [location](../docs/b2c-custom-policy/).
        - In all of the xml files, replace the string `yourtenant` with the name of your Azure AD B2C tenant.

        - For example, if the name of your B2C tenant is `contosotenant`, all instances of `yourtenant.onmicrosoft.com` become `contosotenant.onmicrosoft.com`.
    - Add application IDs to the custom policy
        - Add the application IDs to the extensions file *TrustFrameworkExtensions.xml*.

        1. Open **`TrustFrameworkExtensions.xml`** and find the element `<TechnicalProfile Id="login-NonInteractive">`.
        1. Replace both instances of `IdentityExperienceFrameworkAppId` with the application ID of the IdentityExperienceFramework application that you created earlier.
        1. Replace both instances of `ProxyIdentityExperienceFrameworkAppId` with the application ID of the ProxyIdentityExperienceFramework application that you created earlier.
        1. Find the element `<TechnicalProfile Id="AAD-Common">` and replace `B2CExtensionsAppApplicationId` with your b2c-extensions-app application ID and replace `B2CExtensionsAppObjectId` with your b2c-extensions-app application ObjectId. 
        1. Save the file.  
        <br/>
    - Upload the policies

        1. Select the **Identity Experience Framework** menu item in your B2C tenant in the Azure portal.
        1. Select **Upload custom policy**.
        1. In this order, upload the policy files:
            1. *TrustFrameworkBase.xml*
            2. *TrustFrameworkLocalization.xml*
            3. *TrustFrameworkExtensions.xml*
            4. *SignUpOrSignin.xml*
        