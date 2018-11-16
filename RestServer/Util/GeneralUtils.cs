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
using MongoDB.Driver.GeoJsonObjectModel;
using GeoCoordinatePortable;

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

        public static void CheckSizeForUser(long totalSize, long fileBytesOccupied, long fileBytesLimit)
        {
            if(totalSize + fileBytesOccupied > fileBytesLimit)
            {
                throw new UserLimitException("Limite de armazenamento excedido!");
            }
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

        public static double DistanceBetween(GeoJsonPoint<GeoJson3DGeographicCoordinates> position1, GeoJsonPoint<GeoJson3DGeographicCoordinates> position2)
        {
            if(position1 == null || position2 == null)
            {
                return double.NegativeInfinity;
            }

            var location1 = new GeoCoordinate(position1.Coordinates.Latitude, position1.Coordinates.Longitude);
            var location2 = new GeoCoordinate(position2.Coordinates.Latitude, position2.Coordinates.Longitude);

            return location1.GetDistanceTo(location2);
        }

        public static GeoCoordinate ToGeoCoordinate(this GeoJson2DGeographicCoordinates geoJsonCoordinate)
        {
            return new GeoCoordinate(geoJsonCoordinate.Latitude, geoJsonCoordinate.Longitude);
        }

        public static GeoCoordinate ToGeoCoordinate(this GeoJson3DGeographicCoordinates geoJsonCoordinate)
        {
            return new GeoCoordinate(geoJsonCoordinate.Latitude, geoJsonCoordinate.Longitude, geoJsonCoordinate.Altitude);
        }

        public static GeoJson3DGeographicCoordinates ToGeoJsonCoordinate(this GeoCoordinate geoCoordinate)
        {
            return new GeoJson3DGeographicCoordinates(geoCoordinate.Longitude, geoCoordinate.Latitude, geoCoordinate.Altitude);
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

        public static async Task<string> GenerateRandomBase64(int byteLength = 5)
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

        /// <summary>
        /// Resolves a token to a FileReference, if the token exists for this user.
        /// The action should be called once the calling code can ensure the file is handled ok.
        /// </summary>
        /// <param name="mongoWrapper">For accessing the database</param>
        /// <param name="tokenId">The token ID to search for</param>
        /// <param name="userId">The user ID to correlate with the token</param>
        /// <returns>A Tuple of the FileReference and an async action to expire the token</returns>
        public static async Task<(FileReference, Func<Task>)> GetFileForReferenceToken(MongoWrapper mongoWrapper, string tokenId, ObjectId userId)
        {
            var tokenCollection = mongoWrapper.Database.GetCollection<DataReferenceToken<ConsumableData<ObjectId>>>(nameof(ReferenceToken));

            var tokenFilterBuilder = new FilterDefinitionBuilder<DataReferenceToken<ConsumableData<ObjectId>>>();
            var tokenFilter = tokenFilterBuilder.And
            (
                GeneralUtils.NotDeactivated(tokenFilterBuilder),
                tokenFilterBuilder.Eq(t => t._id, tokenId),
                tokenFilterBuilder.Eq(t => t.UserId, userId)
            );

            var tokenUpdateBuilder = new UpdateDefinitionBuilder<DataReferenceToken<ConsumableData<ObjectId>>>();
            var tokenUpdate = tokenUpdateBuilder
                .Set(t => t.DeactivationDate, DateTime.UtcNow)
                .Set(t => t.AdditionalData.IsConsumed, true);

            DataReferenceToken<ConsumableData<ObjectId>> token = (await tokenCollection.FindAsync(tokenFilter, new FindOptions<DataReferenceToken<ConsumableData<ObjectId>>, DataReferenceToken<ConsumableData<ObjectId>>>
            {
                Limit = 1,
                AllowPartialResults = false,
            })).SingleOrDefault();

            if (token == null)
            {
                throw new ValidationException("Token de Arquivo não encontrado!");
            }

            var gridFsBucket = new GridFSBucket<ObjectId>(mongoWrapper.Database);

            var fileFilterBuilder = new FilterDefinitionBuilder<GridFSFileInfo<ObjectId>>();
            var fileFilter = fileFilterBuilder.Eq(finfo => finfo.Id, token.AdditionalData.Data);

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
                FileInfo = new Models.FileInfo
                {
                    FileMetadata = BsonSerializer.Deserialize<FileMetadata>(fileInfo.Metadata),
                    Size = fileInfo.Length
                },
                _id = token.AdditionalData.Data
            };

            return
            (
                fileReference,
                async () =>
                {
                    var tokenUpdateTask = tokenCollection.UpdateOneAsync
                    (
                        tokenFilter,
                        tokenUpdate
                    );

                    var userCollection = mongoWrapper.Database.GetCollection<User>(nameof(User));

                    var userFilterBuilder = new FilterDefinitionBuilder<User>();
                    var userFilter = userFilterBuilder.And
                    (
                        GeneralUtils.NotDeactivated(userFilterBuilder),
                        userFilterBuilder.Eq(u => u._id, userId)
                    );

                    var userUpdateBuilder = new UpdateDefinitionBuilder<User>();
                    var userUpdate = userUpdateBuilder.Inc(u => u.FileBytesOccupied, fileReference.FileInfo.Size);

                    var userUpdateTask = userCollection.UpdateOneAsync
                    (
                        userFilter,
                        userUpdate
                    );

                    await tokenUpdateTask;
                    await userUpdateTask;
                }
            );
        }
    }
}