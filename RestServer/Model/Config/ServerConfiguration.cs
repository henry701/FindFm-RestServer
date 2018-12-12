using System.Runtime.Serialization;
using AspNetCoreRateLimit;
using Newtonsoft.Json;

namespace RestServer.Model.Config
{
    /// <summary>
    /// The root element of the configuration passed to the server.
    /// </summary>
    /// <threadsafety static="true" instance="true"/>
    [DataContract]
    [JsonObject]
    public sealed class ServerConfiguration
    {
        /// <inheritdoc cref="MongodbConfiguration"/>
        [DataMember(Name = "mongodb", IsRequired = true)]
        [JsonProperty(PropertyName = "mongodb", Required = Required.Always)]
        public MongodbConfiguration Mongodb { get; private set; }

        /// <inheritdoc cref="SmtpConfiguration"/>
        [DataMember(Name = "smtp", IsRequired = true)]
        [JsonProperty(PropertyName = "smtp", Required = Required.Always)]
        public SmtpConfiguration Smtp { get; private set; }

        /// <inheritdoc cref="ListenConfiguration"/>
        [DataMember(Name = "listening", IsRequired = true)]
        [JsonProperty(PropertyName = "listening", Required = Required.Always)]
        public ListenConfiguration Listening { get; private set; }

        /// <inheritdoc cref="RadioCasterConfiguration"/>
        [DataMember(Name = "radio", IsRequired = true)]
        [JsonProperty(PropertyName = "radio", Required = Required.Always)]
        public RadioCasterConfiguration Radio { get; private set; }

        /// <summary>
        /// Whether error traces should be disabled when an error occurs.
        /// Recomended value of <see langword="true"/> for production.
        /// </summary>
        [DataMember(Name = "disableErrorTraces", IsRequired = true)]
        [JsonProperty(PropertyName = "disableErrorTraces", Required = Required.Always)]
        public bool DisableErrorTraces { get; private set; }

        /// <inheritdoc cref="IpRateLimitOptions"/>
        [DataMember(Name = "ipRateLimitOptions", IsRequired = true)]
        [JsonProperty(PropertyName = "ipRateLimitOptions", Required = Required.Always)]
        public IpRateLimitOptions IpRateLimitOptions { get; private set; }

        /// <summary>
        /// Prompt prefix text to be shown in <see cref="FmShell.Shell"/> Console after application boot.
        /// </summary>
        [DataMember(Name = "consolePrompt", IsRequired = true)]
        [JsonProperty(PropertyName = "consolePrompt", Required = Required.Always)]
        public string ConsolePrompt { get; private set; }
    }
}
