using Models;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using RestServer.Util.Extensions;
using System;
using System.Linq;

namespace RestServer.Util
{
    /// <summary>
    /// Application wrapper for MongoDB connection driver.
    /// </summary>
    /// <seealso cref="IMongoDatabase"/>
    /// <seealso cref="IMongoClient"/>
    internal sealed class MongoWrapper
    {
        public IMongoClient MongoClient { get; private set; }
        public IMongoDatabase Database { get; private set; }

        public MongoWrapper(string connectionString, string databaseName)
        {
            var conventionPack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new IgnoreIfNullConvention(true),
            };
            ConventionRegistry.Register("RestServerConventions", conventionPack, any => true);

            MongoClient = new MongoClient(connectionString);

            Database = MongoClient.GetDatabase(databaseName);

            CreateCollections();
            CreateIndexes();
        }

        private void CreateCollections()
        {
            var collectionNames = Database.ListCollectionNames().ToList();
            typeof(Musician).Assembly.GetExportedTypes().Where(mdl =>
                !collectionNames.Contains(mdl.Name) &&
                mdl.HasAttribute<RootEntityAttribute>()
            ).AsParallel().ForAll(async tp =>
            {
                await Database.CreateCollectionAsync(tp.Name);
            });
        }

        private void CreateIndexes()
        {
            CreatePostIndexes();
            CreateUserIndexes();
        }

        private void CreateUserIndexes()
        {
            var userCollection = Database.GetCollection<Models.User>(nameof(User));
            userCollection.Indexes.CreateOne
            (
                new CreateIndexModel<Models.User>
                (
                    new IndexKeysDefinitionBuilder<Models.User>()
                    .Text(u => u.Email)
                    ,
                    new CreateIndexOptions
                    {
                        Background = false,
                        Name = "UserEmailIndex",
                        Unique = true,
                    }
                )
            );
        }

        private void CreatePostIndexes()
        {
            var postCollection = Database.GetCollection<Models.Post>(nameof(Post));
            postCollection.Indexes.CreateOne
            (
                new CreateIndexModel<Models.Post>
                (
                    new IndexKeysDefinitionBuilder<Models.Post>()
                    .Text(p => p.Text)
                    .Text(p => p.Title)
                    .Text(p => p.Poster.FullName)
                    ,
                    new CreateIndexOptions
                    {
                        Background = false,
                        Name = "PostTextIndex",
                        Unique = false,
                    }
                )
            );
        }
    }
}
