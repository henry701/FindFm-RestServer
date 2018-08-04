using System;

namespace RestServer.Infrastructure.Shell
{
    internal sealed class ServerController
    {
        public delegate void ConfigurationReloader();

        public ConfigurationReloader ReloadConfiguration { get; private set; }

        public ServerController(ConfigurationReloader configurationReloader)
        {
            ReloadConfiguration = configurationReloader;
        }
    }
}
