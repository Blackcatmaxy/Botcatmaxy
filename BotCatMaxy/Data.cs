using MongoDB.Bson.Serialization.Attributes;
using Serilog.Sinks.SystemConsole.Themes;
using MongoDB.Bson.Serialization;
using System.Collections.Generic;
using BotCatMaxy.Settings;
using Discord.WebSocket;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using MongoDB.Bson;
using BotCatMaxy;
using Discord;
using System;
using System.Linq;

namespace BotCatMaxy.Data {
    public static class SettingsData {
        public static T LoadFromFile<T>(this IGuild guild, bool createFile = false) {
            var file = default(T);
            var collection = guild.GetCollection(createFile);
            
            if (collection != null) {
                var filter = Builders<BsonDocument>.Filter.Eq("_id", typeof(T).Name);
                using (var cursor = collection.Find(filter).ToCursor()) {
                    var doc = cursor?.FirstOrDefault();
                    if (doc != null) file = BsonSerializer.Deserialize<T>(doc);
                }
                if (createFile && file == null) return (T)Activator.CreateInstance(typeof(T));
            }

            return file;
        }

        public static void SaveToFile<T>(this T file, IGuild guild) {
            var collection = guild.GetCollection(true);
            collection.FindOneAndDelete(Builders<BsonDocument>.Filter.Eq("_id", typeof(T).Name));
            collection.InsertOne(file.ToBsonDocument());
        }

        public static IMongoCollection<BsonDocument> GetInfractionsCollection(this IGuild guild, bool createDir = true) {
            var db = MainClass.dbClient.GetDatabase("Infractions");
            var guildCollection = db.GetCollection<BsonDocument>(guild.Id.ToString());
            var ownerCollection = db.GetCollection<BsonDocument>(guild.OwnerId.ToString());
            if (guildCollection.CountDocuments(new BsonDocument()) > 0) {
                return guildCollection;
            } else if (ownerCollection.CountDocuments(new BsonDocument()) > 0 || createDir) {
                return ownerCollection;
            }

            return null;
        }

        public static List<Infraction> LoadInfractions(this SocketGuildUser user, bool createDir = false) {
            return user?.Id.LoadInfractions(user.Guild, createDir);
        }

        public static List<Infraction> LoadInfractions(this ulong userID, IGuild guild, bool createDir = false) {
            var collection = guild.GetInfractionsCollection(createDir);
            if (collection == null) return null;
            List<Infraction> infractions = null;

            using (var cursor = collection.Find(Builders<BsonDocument>.Filter.Eq("_id", userID)).ToCursor()) {
                var doc = cursor.FirstOrDefault();
                if (doc != null) infractions = BsonSerializer.Deserialize<UserInfractions>(doc).infractions;
            }
            if (infractions == null && createDir) infractions = new List<Infraction>();
            return infractions;
        }

        public static void SaveInfractions(this SocketGuildUser user, List<Infraction> infractions) {
            user.Id.SaveInfractions(user.Guild, infractions);
        }

        public static void SaveInfractions(this ulong userID, IGuild guild, List<Infraction> infractions) {
            var collection = guild.GetInfractionsCollection(true);
            collection.FindOneAndDelete(Builders<BsonDocument>.Filter.Eq("_id", userID));
            collection.InsertOne(new UserInfractions { ID = userID, infractions = infractions }.ToBsonDocument());
        }

        public static UserInfractions LoadHistory(this ulong userID, IGuild guild, bool createDir = false) {
            var collection = guild.GetInfractionsCollection(createDir);
            if (collection == null) return null;
            UserInfractions history = null;

            using (var cursor = collection.Find(Builders<BsonDocument>.Filter.Eq("_id", userID)).ToCursor()) {
                var doc = cursor.FirstOrDefault();
                if (doc != null) history = BsonSerializer.Deserialize<UserInfractions>(doc);
            }
            if (history == null && createDir) history = new UserInfractions { ID = userID };
            if (history != null) history.guild = guild;
            return history;
        }

        public static void Save(this UserInfractions history) {
            var collection = history.guild.GetInfractionsCollection(true);
            collection.FindOneAndDelete(Builders<BsonDocument>.Filter.Eq("_id", history.ID));
            collection.InsertOne(history.ToBsonDocument());
        }
    }

    public class UserInfractions {
        [BsonId]
        public ulong ID = 0;
        [BsonIgnore]
        public IGuild guild;
        public List<IAction> history = new List<IAction>();
        public List<Infraction> infractions = new List<Infraction>();
    }

    public class TempActionList {
        [BsonId]
        public string ID = "TempActionList";
        public List<TempAct> tempBans = new List<TempAct>();
        public List<TempAct> tempMutes = new List<TempAct>();
    }

    public class BadWords {
        public List<BadWord> all;
        public List<BadWord> onlyAlone;
        public List<BadWord> insideWords;
        public List<List<BadWord>> grouped;

        public BadWords(IGuild guild) {
            all = guild.LoadFromFile<BadWordList>().badWords ?? new List<BadWord>();
            onlyAlone = new List<BadWord>();
            insideWords = new List<BadWord>();
            grouped = new List<List<BadWord>>();
            if (all == null) {
                return;
            }
            foreach (BadWord badWord in all) {
                if (badWord.partOfWord) insideWords.Add(badWord);
                else onlyAlone.Add(badWord);

                List<BadWord> group = grouped.Find(list => list.FirstOrDefault() != null && list.First().euphemism == badWord.euphemism);
                if (group != null) {
                    group.Add(badWord);
                } else {
                    grouped.Add(new List<BadWord> { badWord });
                }
            }
        }
    }

    public enum PunishType {
        TempMute,
        TempBan,
        Mute,
        Ban,
    }

    public interface IAction {
        public string Reason { get; }
        public string LogLink { get; }
        public DateTime TimeStamp { get; }
        public ulong Punisher { get; }
    }

    public interface IPunishment : IAction {
        public PunishType PunishType { get; }
        public TimeSpan? Duration { get; }
    }

    public interface IWarning : IAction {
        public float Size { get; }
    }

    public class Warning : IWarning {
        public float Size { get; }

        public string Reason { get; }

        public string LogLink { get; }

        public DateTime TimeStamp { get; }

        public ulong Punisher { get; }

        public Warning(string reason, ulong punisher, float size, string logLink = null) {
            _ = (size > 999 || size < 0.01).AssertWarnAsync("Warn size shouldn't be allowed to be an invalid length");
            Reason = reason;
            Punisher = punisher;
            Size = size;
            if (logLink.NotEmpty()) LogLink = $"[Click Here]({logLink})";
            TimeStamp = DateTime.Now; 
        }
    }

    public class TempPunishWithWarn : Warning, IPunishment{
        public PunishType PunishType { get; }
        public TimeSpan? Duration { get; }

        public TempPunishWithWarn(PunishType punishType, string reason, ulong punisher, TimeSpan duration, float size, string logLink) : base(reason, punisher, size, logLink) {
            Duration = duration;
            if (punishType == PunishType.Ban || punishType == PunishType.Mute) 
                throw new InvalidOperationException("Can't use permament punishtype in TempPunishWithWarn");
            PunishType = punishType;
        }
    }
}
