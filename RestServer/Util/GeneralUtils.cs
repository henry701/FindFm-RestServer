using Newtonsoft.Json;
using NLog;
using System;
using System.IO;
using RestServer.Model.Config;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace RestServer.Util
{
    /// <summary>
    /// Static utility methods for the application.
    /// </summary>
    internal static class GeneralUtils
    {
        private static readonly ILogger LOGGER = LogManager.GetCurrentClassLogger();

        public static async Task<string> GenerateRandomString(int len = 10, IEnumerable<char> allowedChars = null)
        {
            // TODO
            if(allowedChars == null)
            {
                allowedChars = new char[] { 'a' };
            }
            byte[] tokenBytes = new byte[len];
            await Task.Run(() => new RNGCryptoServiceProvider().GetBytes(tokenBytes));
            string token = Convert.ToBase64String(tokenBytes);
            return token;
        }

        public static ServerConfiguration ReadConfiguration(string path)
        {
            string realPath = path ?? Environment.GetEnvironmentVariable("REST_SRV_CONFIG_PATH") ?? "config.json";
            LOGGER.Info("Reading configuration file from {}", realPath);
            ServerConfiguration config;
            try
            {
                Stream configData = File.OpenRead(realPath);
                var serializer = new JsonSerializer();
                config = serializer.Deserialize<ServerConfiguration>(new JsonTextReader(new StreamReader(configData)));
            }
            catch (Exception e)
            {
                throw new ApplicationException("Error while reading configuration data", e);
            }
            return config;
        }
    }
}