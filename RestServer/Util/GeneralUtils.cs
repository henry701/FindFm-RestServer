using Newtonsoft.Json;
using NLog;
using System;
using System.IO;
using RestServer.Model.Config;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using MongoDB.Driver;
using Models;

namespace RestServer.Util
{
    /// <summary>
    /// Static utility methods for the application.
    /// </summary>
    internal static class GeneralUtils
    {
        private static readonly ILogger LOGGER = LogManager.GetCurrentClassLogger();

        public static FilterDefinition<TDocument> NotDeactivated<TDocument>(FilterDefinitionBuilder<TDocument> builder, DateTime? dateTime = null) where TDocument : IActivationAware
        {
            if(!dateTime.HasValue)
            {
                dateTime = DateTime.UtcNow;
            }
            return builder.Or(
                builder.Not(
                    builder.Exists(doc => doc.DeactivationDate)
                ),
                builder.Gt(doc => doc.DeactivationDate, dateTime.Value)
            );
        }

        public static async Task<string> GenerateRandomString(int len = 10, IEnumerable<char> allowedChars = null)
        {
            if(allowedChars == null)
            {
                allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            }
            char[] allowedCharArray = allowedChars.Distinct().ToArray();
            var randomNum = RandomNumberGenerator.Create();
            char[] chars = new char[len];
            byte[] charIndexes = new byte[len];
            await Task.Run(() => randomNum.GetBytes(charIndexes));
            for (int i = 0; i < len; i++)
            {
                chars[i] = allowedCharArray[charIndexes[i] % allowedCharArray.Length];
            }
            return new string(chars);
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