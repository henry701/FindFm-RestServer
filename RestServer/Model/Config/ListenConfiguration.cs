using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace RestServer.Model.Config
{
    public class ListenConfiguration
    {
        /// <summary>
        /// The address on which the application will listen to incoming requests.
        /// </summary>
        [DataMember(Name = "address", IsRequired = true)]
        [JsonProperty(PropertyName = "address", Required = Required.Always)]
        public string Address { get; private set; }

        /// <summary>
        /// The port on which the application will listen to incoming requests.
        /// </summary>
        [DataMember(Name = "port", IsRequired = true)]
        [JsonProperty(PropertyName = "port", Required = Required.Always)]
        public int Port { get; private set; }

        /// <summary>
        /// The base URL path on which the application will listen to incoming requests.
        /// </summary>
        [DataMember(Name = "basePath", IsRequired = true)]
        [JsonProperty(PropertyName = "basePath", Required = Required.Always)]
        public string BasePath { get; private set; }
    }
}