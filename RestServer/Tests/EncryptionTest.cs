using System.Text;
using RestServer.Util;
using Xunit;

namespace RestServer.Tests
{
    /// <summary>
    /// Test class for unit testing of the user password <see cref="Encryption" /> hashing algorithm.
    /// </summary>
    public sealed class EncryptionTest
    {
        /// <summary>
        /// Tests that the encrypted value is not equal to the unencrypted value.
        /// </summary>
        [Fact]
        public void TestEncryption()
        {
            var encrypted = Encryption.Encrypt("potato");
            Assert.NotEmpty(encrypted);
            Assert.NotEqual(encrypted, Encoding.UTF8.GetBytes("potato"));
        }

        [Fact(Timeout = 1000)]
        public void TestEncryptionPerformance()
        {
            Encryption.Encrypt(
                @"A.VeryL0ng|áNdSecure!!Pass_WORD!
                It must not take more than 1 second to encrypt."
            );
        }

        /// <summary>
        /// Tests that the comparison returns <see langword="true"/> for the encrypted value
        /// and its unencrypted counterpart, when done properly
        /// via the <see cref="Encryption.Compare(string, byte[])"/> method.
        /// </summary>
        [Fact]
        public void TestTrueComparison()
        {
            var encrypted = Encryption.Encrypt("potato");
            Assert.True(Encryption.Compare("potato", encrypted));
        }

        /// <summary>
        /// Tests that the comparison returns <see langword="false"/> for an encrypted value
        /// and another unencrypted value, when done properly
        /// via the <see cref="Encryption.Compare(string, byte[])"/> method.
        /// </summary>
        [Fact]
        public void TestFalseComparison()
        {
            var encrypted = Encryption.Encrypt("potato");
            Assert.False(Encryption.Compare("banana", encrypted));
        }

        /// <summary>
        /// Tests that the comparison returns <see langword="false"/> for an encrypted value
        /// and another unencrypted value that looks like the original value, when done properly
        /// via the <see cref="Encryption.Compare(string, byte[])"/> method.
        /// </summary>
        [Fact]
        public void TestRiskyFalseComparison()
        {
            var encrypted = Encryption.Encrypt("potato");
            Assert.False(Encryption.Compare("potató", encrypted));
        }
    }
}
