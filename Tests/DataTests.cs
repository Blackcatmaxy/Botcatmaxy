using BotCatMaxy;
using BotCatMaxy.Cache;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Moderation;
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
    //Forces all tests that inherit from this to run serialized
    //In theory it should work in parallel but seems unstable
    [Collection("Data")]
    public class BaseDataTests : IDisposable
    {
        protected MongoDbRunner runner;
        protected MongoCollectionBase<BsonDocument> collection;
        protected IMongoDatabase settingsDB;
        protected SettingsCache cache;

        public BaseDataTests()
        {
            runner = MongoDbRunner.Start();
            MongoClient client = new MongoClient(runner.ConnectionString);
            MainClass.dbClient = client;
            settingsDB = client.GetDatabase("Settings");
            var database = client.GetDatabase("IntegrationTest");
            collection = (MongoCollectionBase<BsonDocument>)database.GetCollection<BsonDocument>("TestCollection");
        }

        public void Dispose()
        {
            runner.Dispose();
        }
    }

    public class DataTests : BaseDataTests, IDisposable
    {
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

            var ownerCollection = settingsDB.GetCollection<BsonDocument>(guild.OwnerId.ToString());
            ownerCollection.InsertOne(new BsonDocument("Test", "Value"));
            collection = guild.GetCollection(false);
            Assert.NotNull(collection);
            Assert.Equal(guild.OwnerId.ToString(), collection.CollectionNamespace.CollectionName);
            Assert.True(ownerCollection is MongoCollectionBase<BsonDocument>);
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
            Assert.Empty(infractions);
            userID.AddWarn(1, "Test", guild, "link");
            infractions = userID.LoadInfractions(guild, false);
            Assert.NotNull(infractions);
            Assert.NotEmpty(infractions);
        }
    }
}