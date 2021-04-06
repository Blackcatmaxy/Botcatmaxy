using BotCatMaxy.Cache;
using BotCatMaxy.Models;
using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BotCatMaxy.Data
{
    public static class DataManipulator
    {
        static readonly Type cacheType = typeof(GuildSettings);
        static readonly ReplaceOptions replaceOptions = new() { IsUpsert = true };

        public static async Task MapTypes()
        {
            try
            {
                var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
                ConventionRegistry.Register("camelCase", conventionPack, t => true);

                BsonClassMap.RegisterClassMap<ModerationSettings>();
                BsonClassMap.RegisterClassMap<UserInfractions>();
                BsonClassMap.RegisterClassMap<LogSettings>();
                BsonClassMap.RegisterClassMap<Infraction>();
                BsonClassMap.RegisterClassMap<TempAct>();
                BsonClassMap.RegisterClassMap<BadWord>();
            }
            catch (Exception e)
            {
                await new LogMessage(LogSeverity.Critical, "Main", "Unable to map type", e).Log();
            }
        }

        //checks and gets from cache if it's there
        public static T GetFromCache<T>(this IGuild guild, out FieldInfo field, out GuildSettings gCache) where T : DataObject
        {
            string tName = typeof(T).Name;
            tName = char.ToLower(tName[0], CultureInfo.InvariantCulture) + tName.Substring(1);
            field = cacheType.GetField(tName);
            gCache = null;
            if (SettingsCache.guildSettings.Count == 0)
                return null;

            gCache = SettingsCache.guildSettings.FirstOrDefault(g => g.ID == guild.Id);
            if (gCache == null)
                return null;
            return field.GetValue(gCache) as T;
        }

        public static void AddToCache<T>(this T file, FieldInfo field = null, GuildSettings gCache = null) where T : DataObject
        {
            try
            {
                if (field == null)
                {//Figures out where to put file
                    string tName = typeof(T).Name;
                    tName = char.ToLower(tName[0], CultureInfo.InvariantCulture) + tName.Substring(1);
                    field = cacheType.GetField(tName);
                }
                if (file?.guild == null) throw new NullReferenceException("File or Guild is null");
                if (gCache == null && SettingsCache.guildSettings.Count > 0)
                    gCache = SettingsCache.guildSettings.FirstOrDefault(g => g.ID == file.guild.Id);
                if (gCache == null)
                {//This is messy but idea is that if here then there's no guild settings with the right ID
                    SettingsCache.guildSettings.Add(new GuildSettings(file.guild));
                    gCache = SettingsCache.guildSettings.First(g => g.ID == file.guild.Id);
                }
                //Sets cache of file to value
                field.SetValue(gCache, file);
            }
            catch (Exception e)
            {
                new LogMessage(LogSeverity.Critical, "Cache", "Something went wrong with cache", e).Log();
            }
        }

        public static T LoadFromFile<T>(this IGuild guild, bool createFile = false) where T : DataObject
        {
            T file = guild.GetFromCache<T>(out FieldInfo field, out GuildSettings gCache);
            if (file != null) return file;

            var collection = guild.GetCollection(createFile);
            if (collection != null)
            {
                var filter = Builders<BsonDocument>.Filter.Eq("_id", typeof(T).Name);
                using (var cursor = collection.Find(filter).ToCursor())
                {
                    var doc = cursor?.FirstOrDefault();
                    if (doc != null) file = BsonSerializer.Deserialize<T>(doc);
                }

                if (createFile && file == null)
                {
                    file = (T)Activator.CreateInstance(typeof(T));
                    file.guild = guild;
                }
            }

            //small things like set cache
            if (file != null)
            {
                file.guild = guild;
                file.AddToCache(field, gCache);
            }

            return file;
        }

        public static void SaveToFile<T>(this T file) where T : DataObject
        {
            file.AddToCache();
            var collection = file.guild.GetCollection(true);
            var name = typeof(T).Name;
            var filter = Builders<BsonDocument>.Filter.Eq("_id", name);
            var document = file.ToBsonDocument()
                .Set("_id", name);
            collection.ReplaceOne(filter, document, replaceOptions);
        }

        public static IMongoCollection<BsonDocument> GetInfractionsCollection(this IGuild guild, bool createDir = true)
        {
            var db = MainClass.dbClient.GetDatabase("Infractions");
            var guildCollection = db.GetCollection<BsonDocument>(guild.Id.ToString());
            var ownerCollection = db.GetCollection<BsonDocument>(guild.OwnerId.ToString());
            if (guildCollection.CountDocuments(new BsonDocument()) > 0)
                return guildCollection;
            else if (ownerCollection.CountDocuments(new BsonDocument()) > 0)
                return ownerCollection;
            else if (createDir)
                return guildCollection;

            return null;
        }

        public static IMongoCollection<BsonDocument> GetActHistoryCollection(this IGuild guild, bool createDir = true)
        {
            var db = MainClass.dbClient.GetDatabase("ActHistory");
            var guildCollection = db.GetCollection<BsonDocument>(guild.Id.ToString());
            if (guildCollection.CountDocuments(new BsonDocument()) > 0 || createDir)
            {
                return guildCollection as MongoCollectionBase<BsonDocument>;
            }

            return null;
        }

        public static List<Infraction> LoadInfractions(this IGuildUser user, bool createDir = false) =>
            user?.Id.LoadInfractions(user.Guild, createDir);

        public static List<Infraction> LoadInfractions(this UserRef userRef, IGuild guild, bool createDir = false) =>
            userRef?.ID.LoadInfractions(guild, createDir);

        public static List<Infraction> LoadInfractions(this ulong userID, IGuild guild, bool createDir = false)
        {
            var collection = guild.GetInfractionsCollection(createDir);
            if (collection == null) return null;
            List<Infraction> infractions = null;

            using (var cursor = collection.Find(Builders<BsonDocument>.Filter.Eq("_id", userID)).ToCursor())
            {
                var doc = cursor.FirstOrDefault();
                if (doc != null) infractions = BsonSerializer.Deserialize<UserInfractions>(doc).infractions;
            }
            if (infractions == null && createDir) infractions = new List<Infraction>();
            return infractions;
        }

        public static List<ActRecord> LoadActRecord(this ulong userID, IGuild guild, bool createDir = false)
        {
            var collection = guild.GetActHistoryCollection(createDir);
            if (collection == null) return null;
            List<ActRecord> actRecord = null;

            using (var cursor = collection.Find(Builders<BsonDocument>.Filter.Eq("_id", userID)).ToCursor())
            {
                var doc = cursor.FirstOrDefault();
                if (doc != null) actRecord = BsonSerializer.Deserialize<UserActs>(doc).acts;
            }
            if (actRecord == null && createDir) actRecord = new List<ActRecord>();
            return actRecord;
        }
        public static void SaveActRecord(this ulong userID, IGuild guild, List<ActRecord> acts)
        {
            var collection = guild.GetActHistoryCollection(true);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", userID);
            var document = new UserActs { ID = userID, acts = acts }.ToBsonDocument();
            collection.ReplaceOne(filter, document, replaceOptions);
        }

        public static void SaveInfractions(this SocketGuildUser user, List<Infraction> infractions) =>
            user.Id.SaveInfractions(user.Guild, infractions);
        public static void SaveInfractions(this UserRef userRef, List<Infraction> infractions, IGuild guild) =>
            userRef.ID.SaveInfractions(guild, infractions);

        public static void SaveInfractions(this ulong userID, IGuild guild, List<Infraction> infractions)
        {
            var collection = guild.GetInfractionsCollection(true);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", userID);
            var document = new UserInfractions { ID = userID, infractions = infractions }.ToBsonDocument();
            collection.ReplaceOne(filter, document, replaceOptions);
        }
    }

    [BsonIgnoreExtraElements(Inherited = true)]
    public class DataObject
    {
        [BsonIgnore]
        public IGuild guild;
    }
}
