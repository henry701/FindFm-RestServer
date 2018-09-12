using System.IO;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Crypto;
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
                var pemReader = new PemReader(reader);
                var bouncyKey = (AsymmetricCipherKeyPair) pemReader.ReadObject();
                var bouncyPrivateKey = (RsaPrivateCrtKeyParameters) bouncyKey.Private;
                using (var rsa = new RSACryptoServiceProvider())
                {
                    var parameters = new RSAParameters
                    {
                        Modulus = bouncyPrivateKey.Modulus.ToByteArrayUnsigned(),
                        Exponent = bouncyPrivateKey.PublicExponent.ToByteArrayUnsigned(),
                        D = bouncyPrivateKey.Exponent.ToByteArrayUnsigned(),
                        P = bouncyPrivateKey.P.ToByteArrayUnsigned(),
                        Q = bouncyPrivateKey.Q.ToByteArrayUnsigned(),
                        DP = bouncyPrivateKey.DP.ToByteArrayUnsigned(),
                        DQ = bouncyPrivateKey.DQ.ToByteArrayUnsigned(),
                        InverseQ = bouncyPrivateKey.QInv.ToByteArrayUnsigned(),
                    };
                    rsa.ImportParameters(parameters);
                    Key = new RsaSecurityKey(rsa.ExportParameters(true));
                }
            }
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.RsaSha256Signature);
        }
    }
}
