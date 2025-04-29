using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClientAssertionUtility
{
    public class ClientAssertion
    {
        public static void Main()
        {
            UserInputs userInputs = new UserInputs();
            ReadInput(userInputs);
            string jwtToken = ProcessInputAndGenerateJwtToken(userInputs);
            Console.WriteLine("Client Assertion : \n" + jwtToken);
            string jwk = ConvertCertificateToJwk(userInputs);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("JWKS (Json Web Key Set) : \n"+jwk);

            CreateOutputFiles(jwk, jwtToken);
            Console.ReadLine();

        }

        /// <summary>
        /// This method reads input from user and store value in out parameters
        /// </summary>
        private static void ReadInput(UserInputs userInputs)
        {
            try
            {
                Console.WriteLine("Enter client ID of app registration: ");
                userInputs.ClientID = Console.ReadLine();
                Console.WriteLine();
                Console.WriteLine("Enter tenent ID: ");
                userInputs.TenantID = Console.ReadLine();
                Console.WriteLine();
                Console.WriteLine("Enter certificate thumbprint: ");
                userInputs.CertThumbPrint = Console.ReadLine();
                Console.WriteLine();
                Console.WriteLine("Enter .pem folder path: ");
                userInputs.KeyFilePath = Console.ReadLine();
                Console.WriteLine();
                Console.WriteLine("Enter Aud URL");
                userInputs.AudURL = Console.ReadLine();
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Process user input and return jwt token
        /// </summary>
        /// <returns>jwt token</returns>
        private static string ProcessInputAndGenerateJwtToken(UserInputs userInputs)
        {
            var now = DateTimeOffset.UtcNow;
            var tokenHandler = new JwtSecurityTokenHandler();
            //var tokenEndpoint = string.Format("https://login.microsoftonline.com/{0}/oauth2/token", userInputs.TenantID);

            try
            {
                byte[] thumbprintBinary = HexStringToByteArray(userInputs.CertThumbPrint);
                string x5tValue = Convert.ToBase64String(thumbprintBinary);

                var claims = new List<Claim>();
                claims.Add(new Claim("sub", userInputs.ClientID));
                claims.Add(new Claim("iss", userInputs.ClientID));
                claims.Add(new Claim("aud", userInputs.AudURL));
                claims.Add(new Claim("exp", now.AddHours(24).ToUnixTimeSeconds().ToString()));
                claims.Add(new Claim("iat", now.ToUnixTimeSeconds().ToString()));

                var securityKey = new RsaSecurityKey(GetRsaPrivateKey(userInputs.KeyFilePath));
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

                // Create header with x5t (thumbprint)
                var header = new JwtHeader(credentials)
                {
                    { "x5t", x5tValue }
                };

                var payload = new JwtPayload(claims);
                var securityToken = new JwtSecurityToken(header, payload);
                string token = tokenHandler.WriteToken(securityToken);

                return token;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Getting RSA private key
        /// </summary>
        /// <param name="privateKeyPath"></param>
        /// <returns></returns>
        private static RSA GetRsaPrivateKey(string pemFilePath)
        {
            try
            {
                var pem = File.ReadAllText(Directory.EnumerateFiles(pemFilePath, "*.pem").FirstOrDefault());
                var rsa = RSA.Create();
                rsa.ImportFromPem(pem.ToCharArray());
                return rsa;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Converting thumbprint value to byte array
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        static byte[] HexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        private static string ConvertCertificateToJwk(UserInputs userInputs)
        {
            // Read the certificate file
            string certContent = File.ReadAllText(Directory.EnumerateFiles(userInputs.KeyFilePath, "*.pem").FirstOrDefault());

            // Extract CERTIFICATE section
            string certBase64 = string.Empty;
            var certMatch = Regex.Match(certContent, "-----BEGIN CERTIFICATE-----(.*?)-----END CERTIFICATE-----", RegexOptions.Singleline);
            if (certMatch.Success)
            {
                certBase64 = certMatch.Groups[1].Value.Replace("\r", "").Replace("\n", "").Trim();
            }


            byte[] certBytes = Convert.FromBase64String(certBase64);

            // Load the certificate
            var cert = new X509Certificate2(certBytes);

            // Extract the RSA public key
            using var rsa = cert.GetRSAPublicKey();
            if (rsa == null)
                throw new Exception("The certificate does not contain an RSA public key.");

            // Get RSA parameters
            RSAParameters rsaParameters = rsa.ExportParameters(false);

            // Convert modulus (n) and exponent (e) to Base64 URL encoding
            string n = Base64UrlEncoder.Encode(rsaParameters.Modulus);
            string e = Base64UrlEncoder.Encode(rsaParameters.Exponent);

            // Create JWK object
            var jwk = new
            {
                kty = "RSA",
                use = "sig",
                alg = "RS384",
                key_ops = new List<string>() { "verify" },
                kid = Guid.NewGuid().ToString(),
                n = n,
                e = e,
                ext = true
            };

            // Serialize JWK to JSON format
            return JsonSerializer.Serialize(new { keys = new[] { jwk } }, new JsonSerializerOptions { WriteIndented = true });
        }

        private static void CreateOutputFiles(string jwk, string clientAssertion)
        {
            string outputFolder = Path.Combine(AppContext.BaseDirectory, "Output");

            // Ensure Output folder exists
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            // Define file path for jwk.txt
            string filePath = Path.Combine(outputFolder, "jwk.txt");

            // Write JWK content to the file
            File.WriteAllText(filePath, jwk);

            //Define file path for clientAssertion.txt
            filePath = Path.Combine(outputFolder, "clientAssertion.txt");

            // Write file content to the file
            File.WriteAllText(filePath, clientAssertion);
        }
    }
}
