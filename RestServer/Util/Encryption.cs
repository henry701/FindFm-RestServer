using System;
using System.Linq;
using System.Security.Cryptography;

namespace RestServer.Util
{
    /// <summary>
    /// Provides one-way encryption of a <see cref="string"/> into a <see cref="byte"/> array,
    /// and comparison methods to go with it. Uses passed or generated salt, and has configurable
    /// iteration and key size parameters.
    /// </summary>
    internal sealed class Encryption
    {
        /// <summary>
        /// Compares the provided <see cref="string"/> <paramref name="pass"/>
        /// with the provided <paramref name="encryptedPass"/>.
        /// </summary>
        /// <param name="pass">The <see cref="string"/> to be checked.</param>
        /// <param name="encryptedPass">The <see cref="byte"/>[] to be checked.</param>
        /// <returns>Whether the encrypted <paramref name="pass"/> equals <paramref name="encryptedPass"/>.</returns>
        public static bool Compare(string pass, byte[] encryptedPass)
        {
            EncryptedData encryptedData = new EncryptedData(encryptedPass);
            byte[] encryptedPlain = Encrypt(pass, encryptedData.Salt, encryptedData.Iterations);
            return Enumerable.SequenceEqual(encryptedPlain, encryptedPass);
        }

        /// <summary>
        /// Encrypts a <see cref="string"/> with a generated salt and default number of iterations.
        /// </summary>
        /// <param name="pass">The <see cref="string"/> to be encrypted.</param>
        /// <returns>The encrypted <see cref="string"/> as a <see cref="byte"/> array.</returns>
        public static byte[] Encrypt(string pass)
        {
            return Encrypt(pass, MakeSalt(512));
        }

        /// <summary>
        /// Encrypts a <see cref="string"/> with the provided <paramref name="salt"/>.
        /// </summary>
        /// <param name="pass">The <see cref="string"/> to be encrypted.</param>
        /// <param name="salt">The salt to use for the encryption.</param>
        /// <returns>The encrypted <see cref="string"/> as a <see cref="byte"/> array.</returns>
        public static byte[] Encrypt(string pass, byte[] salt)
        {
            return Encrypt(pass, salt, 5000);
        }

        /// <summary>
        /// Encrypts a <see cref="string"/> with the provided <paramref name="salt"/>.
        /// </summary>
        /// <param name="pass">The <see cref="string"/> to be encrypted.</param>
        /// <param name="salt">The salt to use for the encryption.</param>
        /// <param name="iterations">The number of iterations to run the encryption</param>
        /// <returns>The encrypted <see cref="string"/> as a <see cref="byte"/> array.</returns>
        public static byte[] Encrypt(string pass, byte[] salt, int iterations)
        {
            return Encrypt(pass, salt, iterations, 512);
        }

        /// <summary>
        /// Encrypts a <see cref="string"/> with the provided <paramref name="salt"/>.
        /// </summary>
        /// <param name="pass">The <see cref="string"/> to be encrypted.</param>
        /// <param name="salt">The salt to use for the encryption.</param>
        /// <param name="iterations">The number of iterations to run the encryption</param>
        /// <param name="keySize">The size for the key part of the generated <see cref="byte"/>[]</param>
        /// <returns>The encrypted <see cref="string"/> as a <see cref="byte"/> array.</returns>
        public static byte[] Encrypt(string pass, byte[] salt, int iterations, int keySize)
        {
            var rfc = new Rfc2898DeriveBytes(pass, salt, iterations);
            byte[] key = rfc.GetBytes(keySize);
            return new EncryptedData(key, salt, iterations).GetBytes();
        }

        private static byte[] MakeSalt(int saltSize)
        {
            byte[] salt = new byte[saltSize];
            new RNGCryptoServiceProvider().GetBytes(salt);
            // If not Little Endian, reverse, because they should be.
            if (!BitConverter.IsLittleEndian)
            {
                salt = salt.Reverse().ToArray();
            }
            return salt;
        }

        private class EncryptedData
        {
            public int Iterations { get; set; }
            public byte[] Salt { get; set; }
            public byte[] Key { get; set; }

            public EncryptedData(byte[] res)
            {
                byte[] iterationsBytes = res.Take(sizeof(int)).ToArray();
                byte[] saltSizeBytes = res.Skip(sizeof(int)).Take(sizeof(int)).ToArray();
                // If not Little Endian, reverse, because the data is in little endian.
                if (!BitConverter.IsLittleEndian)
                {
                    iterationsBytes = iterationsBytes.Reverse().ToArray();
                    saltSizeBytes = saltSizeBytes.Reverse().ToArray();
                }
                Iterations = BitConverter.ToInt32(iterationsBytes, 0);
                int saltSize = BitConverter.ToInt32(saltSizeBytes, 0);
                Salt = new byte[saltSize];
                Array.Copy(res, sizeof(int) * 2, Salt, 0, Salt.Length);
                Key = new byte[res.Length - (saltSize + sizeof(int) * 2)];
                Array.Copy(res, sizeof(int) * 2 + Salt.Length, Key, 0, Key.Length);
            }

            public EncryptedData(byte[] key, byte[] salt, int iterations)
            {
                Key = key;
                Salt = salt;
                Iterations = iterations;
            }

            public byte[] GetBytes()
            {
                byte[] iterationBytes = BitConverter.GetBytes(Iterations);
                byte[] saltLengthBytes = BitConverter.GetBytes(Salt.Length);
                // If not Little Endian, reverse, because they should be.
                if (!BitConverter.IsLittleEndian)
                {
                    iterationBytes = iterationBytes.Reverse().ToArray();
                    saltLengthBytes = saltLengthBytes.Reverse().ToArray();
                }
                // Array Layout: iterations, salt length, salt data, key data.
                byte[] res = new byte[sizeof(int) + sizeof(int) + Salt.Length + Key.Length];
                Array.Copy(iterationBytes, 0, res, 0, iterationBytes.Length);
                Array.Copy(saltLengthBytes, 0, res, iterationBytes.Length, saltLengthBytes.Length);
                Array.Copy(Salt, 0, res, iterationBytes.Length + saltLengthBytes.Length, Salt.Length);
                Array.Copy(Key, 0, res, iterationBytes.Length + saltLengthBytes.Length + Salt.Length, Key.Length);
                return res;
            }
        }
    }
}
