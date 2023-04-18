# Backend Services Client

- Generation and publishing of JWKS Key Set
- Runtime signing of the client assestion JWT

## Generation and publishing of JWKS Key Set

- Store the private in KeyVault
- JWKS: public azure storage container?

1. Generate the private key in Azure KeyVault
  - Add a key
  - RSA 2048 

![CleanShot 2023-04-18 at 11 31 17](https://user-images.githubusercontent.com/753437/232870701-b9553829-4309-43f6-afdf-ee123a93b374.png)


2. Get the public key in C#

```c#
RSA rsa1 = RSA.Create(2048);

RsaSecurityKey publicKey1 = new(rsa1.ExportParameters(false))
{
    KeyId = "keyId1"
};

RsaSecurityKey publicAndPrivateKey1 = new(rsa1.ExportParameters(true))
{
    KeyId = "keyId1"
};

```

3. Generate JWKS.

```c#
IList<JsonWebKey> jwksList = new List<JsonWebKey>
{
    jwk1,
};

Dictionary<string, IList<JsonWebKey>> jwksDict = new() 
{ 
    { "keys", jwksList }
};

string jwksStr = SerializeToJson(jwksDict);
```

4. Upload the `jwksSrt` to public storage.

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
