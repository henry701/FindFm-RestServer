using Models;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using RestServer.Util.Extensions;
using System;
using System.Linq;
using System.Threading.Tasks;

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
            Task.WaitAll(RegisterConventions());
            MongoClient = new MongoClient(connectionString);
            Database = MongoClient.GetDatabase(databaseName);
            Task.WaitAll(CreateCollections());
            Task.WaitAll(CreateIndexes());
        }

        private static Task RegisterConventions()
        {
            var conventionPack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new IgnoreIfNullConvention(true),
            };
            ConventionRegistry.Register("RestServerConventions", conventionPack, any => true);
            return Task.CompletedTask;
        }

        private async Task CreateCollections()
        {
            var collectionNames = (await Database.ListCollectionNamesAsync()).ToList();
            typeof(Musician).Assembly.GetExportedTypes().Where(mdl =>
                !collectionNames.Contains(mdl.Name) &&
                mdl.HasAttribute<RootEntityAttribute>()
            ).AsParallel().ForAll(async tp =>
            {
                await Database.CreateCollectionAsync(tp.Name);
            });
        }

        private async Task CreateIndexes()
        {
            await CreatePostIndexes();
            await CreateUserIndexes();
        }

        private async Task CreateUserIndexes()
        {
            var userCollection = Database.GetCollection<Models.User>(nameof(User));
            await userCollection.Indexes.CreateManyAsync
            (
                new[]
                {
                    new CreateIndexModel<Models.User>
                    (
                        new IndexKeysDefinitionBuilder<Models.User>()
                        .Ascending(u => u.Email)
                        ,
                        new CreateIndexOptions
                        {
                            Background = false,
                            Name = "UserEmailIndex",
                            Unique = true,
                        }
                    ),
                    new CreateIndexModel<Models.User>
                    (
                        new IndexKeysDefinitionBuilder<Models.User>()
                        .Text(u => u.FullName)
                        .Text(u => u.About)
                        .Text($"{nameof(Musician.InstrumentSkills).WithLowercaseFirstCharacter()}.skill")
                        ,
                        new CreateIndexOptions
                        {
                            Background = false,
                            Name = "UserTextIndex",
                            Unique = false,
                        }
                    )
                }
            );
        }

        private async Task CreatePostIndexes()
        {
            var postCollection = Database.GetCollection<Models.Post>(nameof(Post));
            await postCollection.Indexes.CreateManyAsync
            (
                new[]
                {
                    new CreateIndexModel<Models.Post>
                    (
                        new IndexKeysDefinitionBuilder<Models.Post>()
                        .Text(p => p.Text)
                        .Text(p => p.Title)
                        .Text(p => p.Poster.FullName)
                        .Text(p => p.Poster.About)
                        ,
                        new CreateIndexOptions
                        {
                            Background = false,
                            Name = "PostTextIndex",
                            Unique = false,
                        }
                    )
                }
            );
        }
    }
}
