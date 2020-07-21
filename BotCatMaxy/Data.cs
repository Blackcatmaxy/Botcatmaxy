using MongoDB.Bson.Serialization.Attributes;
using Serilog.Sinks.SystemConsole.Themes;
using MongoDB.Bson.Serialization;
using System.Collections.Generic;
using Discord.WebSocket;
using Discord.Commands;
using System.Reflection;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using BotCatMaxy.Cache;
using MongoDB.Bson;
using System.Linq;
using BotCatMaxy;
using Discord;
using System;
using System.Globalization;
using BotCatMaxy.Models;
using System.Threading.Tasks;

namespace BotCatMaxy.Data
{
    public static class DataManipulator
    {
        static readonly Type cacheType = typeof(GuildSettings);

        public static async Task MapTypes()
        {
            try
            {
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
            if (guild == null)
                throw new ArgumentNullException(nameof(guild));

            string tName = typeof(T).Name;
            tName = char.ToLower(tName[0], CultureInfo.InvariantCulture) + tName.Substring(1);
            field = cacheType.GetField(tName);
            gCache = null;
            if (SettingsCache.guildSettings == null || SettingsCache.guildSettings.Count == 0) return null;
            gCache = SettingsCache.guildSettings.FirstOrDefault(g => g.ID == guild.Id);
            if (gCache == null) return null;
            object cached = field.GetValue(gCache);
            return cached as T;
        }

        public static void AddToCache<T>(this T file, FieldInfo field = null, GuildSettings gCache = null) where T : DataObject
        {
            try
            {
                if (field == null)
                {
                    string tName = typeof(T).Name;
                    tName = char.ToLower(tName[0], CultureInfo.InvariantCulture) + tName.Substring(1);
                    field = cacheType.GetField(tName);
                }
                if (file == null || file.guild == null) throw new NullReferenceException();
                if (gCache == null && SettingsCache.guildSettings != null && SettingsCache.guildSettings.Count > 0)
                {
                    if (file?.guild?.Id == null || SettingsCache.guildSettings.Any(g => g?.ID == null)) throw new NullReferenceException();
                    SettingsCache.guildSettings.FirstOrDefault(g => g.ID == file.guild.Id);
                }
                if (gCache == null)
                {
                    SettingsCache.guildSettings.Add(new GuildSettings(file.guild));
                    gCache = SettingsCache.guildSettings.First(g => g.ID == file.guild.Id);
                }
                if (field == null) throw new NullReferenceException();
                field.SetValue(gCache, file);
            }
            catch (Exception e)
            {
                new LogMessage(LogSeverity.Error, "Cache", "Something went wrong with cache", e).Log();
            }
        }

        public static T LoadFromFile<T>(this IGuild guild, bool createFile = false) where T : DataObject
        {
            if (guild == null)
                throw new ArgumentNullException(nameof(guild));

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
                    return file;
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
            if (file.guild == null) throw new InvalidOperationException("Data file does not have a guild");

            file.AddToCache();
            var collection = file.guild.GetCollection(true);
            collection.FindOneAndDelete(Builders<BsonDocument>.Filter.Eq("_id", typeof(T).Name));
            collection.InsertOne(file.ToBsonDocument());
        }

        public static IMongoCollection<BsonDocument> GetInfractionsCollection(this IGuild guild, bool createDir = true)
        {
            var db = MainClass.dbClient.GetDatabase("Infractions");
            var guildCollection = db.GetCollection<BsonDocument>(guild.Id.ToString());
            var ownerCollection = db.GetCollection<BsonDocument>(guild.OwnerId.ToString());
            if (guildCollection.CountDocuments(new BsonDocument()) > 0)
            {
                return guildCollection;
            }
            else if (ownerCollection.CountDocuments(new BsonDocument()) > 0 || createDir)
            {
                return ownerCollection;
            }

            return null;
        }

        public static IMongoCollection<BsonDocument> GetActHistoryCollection(this IGuild guild, bool createDir = true)
        {
            var db = MainClass.dbClient.GetDatabase("ActHistory");
            var guildCollection = db.GetCollection<BsonDocument>(guild.Id.ToString());
            if (guildCollection.CountDocuments(new BsonDocument()) > 0 || createDir)
            {
                return guildCollection;
            }

            return null;
        }

        public static List<Infraction> LoadInfractions(this SocketGuildUser user, bool createDir = false) =>
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
            collection.FindOneAndDelete(Builders<BsonDocument>.Filter.Eq("_id", userID));
            collection.InsertOne(new UserActs { ID = userID, acts = acts }.ToBsonDocument());
        }

        public static void SaveInfractions(this SocketGuildUser user, List<Infraction> infractions) =>
            user.Id.SaveInfractions(user.Guild, infractions);
        public static void SaveInfractions(this UserRef userRef, List<Infraction> infractions, IGuild guild) =>
            userRef.ID.SaveInfractions(guild, infractions);

        public static void SaveInfractions(this ulong userID, IGuild guild, List<Infraction> infractions)
        {
            var collection = guild.GetInfractionsCollection(true);
            collection.FindOneAndDelete(Builders<BsonDocument>.Filter.Eq("_id", userID));
            collection.InsertOne(new UserInfractions { ID = userID, infractions = infractions }.ToBsonDocument());
        }
    }

    public class DataObject
    {
        [BsonIgnore]
        public IGuild guild;
    }
}
