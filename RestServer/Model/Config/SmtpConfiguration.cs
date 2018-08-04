using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace RestServer.Model.Config
{
    /// <summary>
    /// Configuration for usage of SMTP by the application to send e-mails.
    /// </summary>
    [DataContract]
    [JsonObject]
    public sealed class SmtpConfiguration
    {
        /// <summary>
        /// Host of the SMTP server.
        /// </summary>
        [DataMember(Name = "host", IsRequired = true)]
        [JsonProperty(PropertyName = "host", Required = Required.Always)]
        public string Host { get; private set; }

        /// <summary>
        /// Timeout while connecting to SMTP server.
        /// </summary>
        [DataMember(Name = "timeout", IsRequired = true)]
        [JsonProperty(PropertyName = "timeout", Required = Required.Always)]
        public int Timeout { get; private set; }

        /// <summary>
        /// Port to connect on SMTP server.
        /// </summary>
        [DataMember(Name = "port", IsRequired = true)]
        [JsonProperty(PropertyName = "port", Required = Required.Always)]
        public int Port { get; private set; }

        /// <summary>
        /// From e-mail, doubling as login credentials for the SMTP server.
        /// </summary>
        [DataMember(Name = "email", IsRequired = true)]
        [JsonProperty(PropertyName = "email", Required = Required.Always)]
        public string Email { get; private set; }

        /// <summary>
        /// Display name in from email, in case the server allows it to be different than the <see cref="Email"/>.
        /// </summary>
        [DataMember(Name = "displayName", IsRequired = true)]
        [JsonProperty(PropertyName = "displayName", Required = Required.Always)]
        public string DisplayName { get; private set; }

        /// <summary>
        /// Password for authenticating with the SMTP server.
        /// </summary>
        [DataMember(Name = "password", IsRequired = true)]
        [JsonProperty(PropertyName = "password", Required = Required.Always)]
        public string Password { get; private set; }
    }
}
