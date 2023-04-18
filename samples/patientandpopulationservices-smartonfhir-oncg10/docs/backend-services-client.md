# Backend Services Client

- Generation and publishing of JWKS Key Set
- Runtime signing of the client assestion JWT

## Generation and publishing of JWKS Key Set

- Store the private in KeyVault
- JWKS: public azure storage container?

1. Generate the private key

## Runtime signing of the client assestion JWT

When signing, you have two options:
- Sign with KeyVault
- Sign in your application

KeyVault has [throttling for signing](https://learn.microsoft.com/azure/key-vault/general/overview-throttling), may be best to do in your client app or elsewhere.

To sign yourself


```c#
        string GetSignedClientAssertionAlt(X509Certificate2 certificate)
        {
            //aud = https://login.microsoftonline.com/ + Tenant ID + /v2.0
            string aud = $"https://login.microsoftonline.com/{tenantID}/v2.0";

            // client_id
            string confidentialClientID = "00000000-0000-0000-0000-000000000000";

            // no need to add exp, nbf as JsonWebTokenHandler will add them by default.
            var claims = new Dictionary<string, object>()
            {
                { "aud", aud },
                { "iss", confidentialClientID },
                { "jti", Guid.NewGuid().ToString() },
                { "sub", confidentialClientID }
            };

            var securityTokenDescriptor = new SecurityTokenDescriptor
            {
                Claims = claims,
                SigningCredentials = new X509SigningCredentials(certificate)
            };

            var handler = new JsonWebTokenHandler();
            var signedClientAssertion = handler.CreateToken(securityTokenDescriptor);
        }
```
https://learn.microsoft.com/en-us/azure/active-directory/develop/msal-net-client-assertions#alternative-method
