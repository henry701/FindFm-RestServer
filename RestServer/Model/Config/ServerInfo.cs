using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Hosting;

namespace RestServer.Model.Config
{
    public sealed class ServerInfo
    {
        public string ConfigurationPath { get; private set; }
        public string HostUri { get; private set; }

        public ServerInfo(string configurationPath, string hostUri)
        {
            ConfigurationPath = configurationPath;
            HostUri = hostUri;
        }
    }
}
