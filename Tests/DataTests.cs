using BotCatMaxy;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.Mocks.Guild;
using Xunit;

namespace Tests
{
    public class DataTests : IDisposable
    {
        protected MongoDbRunner runner;
        protected MongoCollectionBase<BsonDocument> collection;
        protected IMongoDatabase settings;

        public DataTests()
        {
            runner = MongoDbRunner.Start();

            MongoClient client = new MongoClient(runner.ConnectionString);
            MainClass.dbClient = client;
            settings = client.GetDatabase("Settings");
            var database = client.GetDatabase("IntegrationTest");
            collection = (MongoCollectionBase<BsonDocument>)database.GetCollection<BsonDocument>("TestCollection");
        }

        public void Dispose()
        {
            runner.Dispose();
        }

        [Fact]
        public void TestBasic()
        {
            Assert.False(collection.FindSync((Builders<BsonDocument>.Filter.Eq("_id", 1234))).Any());
            var infractions = new UserInfractions()
            {
                ID = 1234,
                infractions = new List<Infraction>
                    {
                        new Infraction() { Reason = "Test", Time = DateTime.UtcNow }
                    }
            };
            collection.InsertOne(infractions.ToBsonDocument());

            var document = collection.FindSync((Builders<BsonDocument>.Filter.Eq("_id", 1234))).FirstOrDefault();
            Assert.NotNull(document);
            Assert.False(collection.FindSync((Builders<BsonDocument>.Filter.Eq("_id", 123))).Any());
        }

        [Fact]
        public void TestCollections()
        {
            var guild = new MockGuild();
            Assert.Null(guild.GetCollection(false));
            var collection = guild.GetCollection(true);
            Assert.NotNull(collection);
            Assert.Equal(guild.Id.ToString(), collection.CollectionNamespace.CollectionName);

            var ownerCollection = settings.GetCollection<BsonDocument>(guild.OwnerId.ToString());
            ownerCollection.InsertOne(new BsonDocument());
            collection = guild.GetCollection(false);
            Assert.NotNull(collection);
            Assert.Equal(guild.OwnerId.ToString(), collection.CollectionNamespace.CollectionName);
        }

        [Fact]
        public void TestInfractions()
        {
            var guild = new MockGuild();
            ulong userID = 12345;
            var infractions = userID.LoadInfractions(guild, false);
            Assert.Null(infractions);
            infractions = userID.LoadInfractions(guild, true);
            Assert.NotNull(infractions);
        }
    }
}