﻿using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace RestServer.Model.Config
{
    public sealed class RadioCasterConfiguration
    {
        /// <summary>
        /// The address on which the radio will listen to incoming requests.
        /// </summary>
        [DataMember(Name = "address", IsRequired = true)]
        [JsonProperty(PropertyName = "address", Required = Required.Always)]
        public string Address { get; private set; }

        /// <summary>
        /// The port on which the radio will listen to incoming requests.
        /// </summary>
        [DataMember(Name = "port", IsRequired = true)]
        [JsonProperty(PropertyName = "port", Required = Required.Always)]
        public int Port { get; private set; }

        /// <summary>
        /// The metadata interval for sending IcyCast metadata.
        /// </summary>
        [DataMember(Name = "icyMetadataInterval", IsRequired = true)]
        [JsonProperty(PropertyName = "icyMetadataInterval", Required = Required.Always)]
        public int IcyMetadataInterval { get; private set; }
    }
}