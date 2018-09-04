using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;

namespace RestServer.Infrastructure.AspNetCore
{
    internal sealed class SigningConfigurations
    {
        public SecurityKey Key { get; }
        public SigningCredentials SigningCredentials { get; }

        public SigningConfigurations(string fileName)
        {
            using (var reader = File.OpenText(fileName))
            {
                var pem = new PemReader(reader);
                var o = (RsaKeyParameters) pem.ReadObject();
                using (var rsa = new RSACryptoServiceProvider())
                {
                    var parameters = new RSAParameters
                    {
                        Modulus = o.Modulus.ToByteArray(),
                        Exponent = o.Exponent.ToByteArray()
                    };
                    rsa.ImportParameters(parameters);
                    Key = new RsaSecurityKey(rsa.ExportParameters(true));
                }
            }
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.RsaSha256Signature);
        }
    }
}
