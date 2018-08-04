using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

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
                new IgnoreIfNullConvention(true)
            };
            ConventionRegistry.Register("RestServerConventions", conventionPack, any => true);

            MongoClient = new MongoClient(connectionString);

            Database = MongoClient.GetDatabase(databaseName);

            // Listing databases just to make the lazy driver
			// actually tries a connection before returning
            MongoClient.ListDatabases();
        }
    }
}
