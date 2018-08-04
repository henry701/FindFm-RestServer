using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace RestServer.Model.Config
{
    /// <summary>
    /// Configuration sections regarding MongoDB parameters.
    /// </summary>
    [DataContract]
    [JsonObject]
    public sealed class MongodbConfiguration
    {
        /// <summary>
        /// The connection string for connecting with the database.
        /// </summary>
        [DataMember(Name = "connectionString", IsRequired = true)]
        [JsonProperty(PropertyName = "connectionString", Required = Required.Always)]
        public string MongoConnectionString { get; private set; }

        /// <summary>
        /// The database to choose after connecting.
        /// </summary>
        [DataMember(Name = "database", IsRequired = true)]
        [JsonProperty(PropertyName = "database", Required = Required.Always)]
        public string MongoDatabase { get; private set; }
    }
}
