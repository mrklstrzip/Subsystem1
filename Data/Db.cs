using System.Configuration;
using MongoDB.Driver;

namespace Subsystem1.Data
{
    public static class Db
    {
        private static string connectionString =
            ConfigurationManager.ConnectionStrings["MongoDb"].ConnectionString;

        private static MongoClient client = new MongoClient(connectionString);

        public static IMongoDatabase Database = client.GetDatabase("Sia_set1_db");
    }
}