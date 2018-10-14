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
using MongoDB.Bson;
using RestServer.Exceptions;
using MongoDB.Driver.GridFS;
using MongoDB.Bson.Serialization;
using System.Linq.Expressions;

namespace RestServer.Util
{
    /// <summary>
    /// Static utility methods for the application.
    /// </summary>
    internal static class GeneralUtils
    {
        private static readonly ILogger LOGGER = LogManager.GetCurrentClassLogger();

        public static FilterDefinition<TDocument> NotDeactivated<TDocument, TItem>(FilterDefinitionBuilder<TDocument> builder, Expression<Func<TDocument, IEnumerable<TItem>>> acessor, DateTime? dateTime = null) where TItem : IActivationAware
        {
            var twoBuilder = new FilterDefinitionBuilder<TItem>();
            if (!dateTime.HasValue)
            {
                dateTime = DateTime.UtcNow;
            }
            return builder.Or
            (
                builder.Not
                (
                    builder.ElemMatch
                    (
                        acessor,
                        twoBuilder.Exists
                        (
                            t => t.DeactivationDate
                        )
                    )
                ),
                builder.ElemMatch
                (
                    acessor,
                    twoBuilder.Gt
                    (
                        t => t.DeactivationDate,
                        dateTime.Value
                    )
                )
            );
        }

        public static FilterDefinition<TDocument> NotDeactivated<TDocument, TItem>(FilterDefinitionBuilder<TDocument> builder, Expression<Func<TDocument, TItem>> acessor, DateTime? dateTime = null) where TItem : IActivationAware
        {
            if (!dateTime.HasValue)
            {
                dateTime = DateTime.UtcNow;
            }
            return builder.Or
            (
                builder.Not
                (
                    builder.Exists
                    (
                        Expression.Lambda<Func<TDocument, object>>
                        (
                            Expression.Convert
                            (
                                Expression.Property
                                (
                                    acessor.Body,
                                    nameof(IActivationAware.DeactivationDate)
                                ),
                                typeof(object)
                            ),
                            acessor.Parameters
                        )
                    )
                ),
                builder.Gt
                (
                    Expression.Lambda<Func<TDocument, DateTime>>
                    (
                        Expression.Convert
                        (
                            Expression.Property
                            (
                                acessor.Body,
                                nameof(IActivationAware.DeactivationDate)
                            ),
                            typeof(DateTime)
                        ),
                        acessor.Parameters
                    ),
                    dateTime.Value
                )
            );
        }

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

        public static async Task<string> GenerateRandomBase64(int byteLength = 512)
        {
            byte[] tokenBytes = new byte[byteLength];
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
                StreamReader streamReader = new StreamReader(configData);

                string configString = streamReader.ReadToEnd();
                configString = Environment.ExpandEnvironmentVariables(configString);

                var serializer = new JsonSerializer();
                config = serializer.Deserialize<ServerConfiguration>
                (
                    new JsonTextReader(new StringReader(configString))
                );
            }
            catch (Exception e)
            {
                throw new ApplicationException("Error while reading configuration data", e);
            }
            return config;
        }

        public static async Task<FileReference> ConsumeReferenceTokenFile(MongoWrapper mongoWrapper, string tokenId, ObjectId userId)
        {
            var tokenCollection = mongoWrapper.Database.GetCollection<ReferenceToken>(nameof(ReferenceToken));

            var tokenFilterBuilder = new FilterDefinitionBuilder<ReferenceToken>();
            var tokenFilter = tokenFilterBuilder.And(
                GeneralUtils.NotDeactivated(tokenFilterBuilder),
                tokenFilterBuilder.Eq(t => t._id, tokenId),
                tokenFilterBuilder.Eq(t => t.UserId, userId)
            );

            var tokenUpdateBuilder = new UpdateDefinitionBuilder<ReferenceToken>();
            var tokenUpdate = tokenUpdateBuilder.Set(t => t.DeactivationDate, DateTime.UtcNow);

            DataReferenceToken<ObjectId> token = await tokenCollection.FindOneAndUpdateAsync(
                tokenFilter,
                tokenUpdate,
                new FindOneAndUpdateOptions<ReferenceToken, DataReferenceToken<ObjectId>>
                {
                    ReturnDocument = ReturnDocument.Before,
                }
            );

            if (token == null)
            {
                throw new ValidationException("Arquivo não encontrado para persistência!");
            }

            var gridFsFileId = token.AdditionalData;

            var gridFsBucket = new GridFSBucket<ObjectId>(mongoWrapper.Database);

            var fileFilterBuilder = new FilterDefinitionBuilder<GridFSFileInfo<ObjectId>>();
            var fileFilter = fileFilterBuilder.Eq(finfo => finfo.Id, gridFsFileId);

            var fileInfo = (await gridFsBucket.FindAsync(fileFilter, new GridFSFindOptions<ObjectId>
            {
                Limit = 1,
            })).SingleOrDefault();

            if(fileInfo == null)
            {
                throw new ApplicationException("Arquivo não encontrado, mas havia um token ativado referenciando-o!");
            }

            var fileReference = new FileReference
            {
                FileMetadata = BsonSerializer.Deserialize<FileMetadata>(fileInfo.Metadata),
                _id = gridFsFileId
            };

            var fileReferenceCollection = mongoWrapper.Database.GetCollection<FileReference>(nameof(FileReference));

            await fileReferenceCollection.InsertOneAsync(fileReference);

            return fileReference;
        }
    }
}