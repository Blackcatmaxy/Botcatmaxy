using MongoDB.Bson.Serialization.Attributes;
using Serilog.Sinks.SystemConsole.Themes;
using MongoDB.Bson.Serialization;
using System.Collections.Generic;
using Discord.WebSocket;
using Discord.Commands;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Linq;
using BotCatMaxy;
using Discord;
using System;

namespace BotCatMaxy.Data {
    public static class DataManipulator {
        public static T LoadFromFile<T>(this IGuild guild, bool createFile = false) where T: DataObject {
            if (guild == null)
                throw new ArgumentNullException(nameof(guild));
            var file = default(T);
            var collection = guild.GetCollection(createFile);
            
            if (collection != null) {
                var filter = Builders<BsonDocument>.Filter.Eq("_id", typeof(T).Name);
                using (var cursor = collection.Find(filter).ToCursor()) {
                    var doc = cursor?.FirstOrDefault();
                    if (doc != null) file = BsonSerializer.Deserialize<T>(doc);
                }
                if (createFile && file == null) {
                    var thing = (T)Activator.CreateInstance(typeof(T));
                    thing.guild = guild;
                    return thing;
                }
            }
            if (file != null) file.guild = guild;
            return file;
        }

        public static void SaveToFile<T>(this T file) where T : DataObject {
            if (file.guild == null) throw new InvalidOperationException("Data file does not have a guild");
            var collection = file.guild.GetCollection(true);
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

        public static List<Infraction> LoadInfractions(this SocketGuildUser user, bool createDir = false) => 
            user?.Id.LoadInfractions(user.Guild, createDir);
        

        public static List<Infraction> LoadInfractions(this UserRef userRef, IGuild guild, bool createDir = false) =>
            userRef?.ID.LoadInfractions(guild, createDir);

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

        public static void SaveInfractions(this SocketGuildUser user, List<Infraction> infractions) => 
            user.Id.SaveInfractions(user.Guild, infractions);
        public static void SaveInfractions(this UserRef userRef, List<Infraction> infractions, IGuild guild) =>
            userRef.ID.SaveInfractions(guild, infractions);

        public static void SaveInfractions(this ulong userID, IGuild guild, List<Infraction> infractions) {
            var collection = guild.GetInfractionsCollection(true);
            collection.FindOneAndDelete(Builders<BsonDocument>.Filter.Eq("_id", userID));
            collection.InsertOne(new UserInfractions { ID = userID, infractions = infractions }.ToBsonDocument());
        }
    }

    public class DataObject {
        [BsonIgnore]
        public IGuild guild;
    }

    //Data classes
    public class Infraction {
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime time;
        public string logLink;
        public string reason;
        public float size;
    }

    public class UserInfractions {
        [BsonId]
        public ulong ID = 0;
        public List<Infraction> infractions = new List<Infraction>();
        public BsonDocument CatchAll { get; set; }
    }

    public class TempActionList : DataObject {
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
            if (guild == null) throw new ArgumentNullException();
            all = guild.LoadFromFile<BadWordList>()?.badWords;
            if (all == null) return;
            onlyAlone = new List<BadWord>();
            insideWords = new List<BadWord>();
            grouped = new List<List<BadWord>>();
            
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

    public class TempAct {
        public TempAct(ulong userID, TimeSpan length, string reason) {
            user = userID;
            this.reason = reason;
            dateBanned = DateTime.Now;
            this.length = length;
        }
        public TempAct(UserRef userRef, TimeSpan length, string reason) {
            user = userRef.ID;
            this.reason = reason;
            dateBanned = DateTime.Now;
            this.length = length;
        }

        public string reason;
        public ulong user;
        public TimeSpan length;
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime dateBanned;
    }

    [BsonIgnoreExtraElements]
    public class BadWord {
        public string word;
        public string euphemism;
        public float size = 0.5f;
        public bool partOfWord = true;
        public object moreWords;
    }

    [BsonIgnoreExtraElements]
    public class BadWordList : DataObject {
        [BsonId]
        public string Id = "BadWordList";
        public List<BadWord> badWords = new List<BadWord>();
    }

    public class ModerationSettings : DataObject {
        [BsonId]
        public string Id = "ModerationSettings";
        public List<ulong> ableToWarn = new List<ulong>();
        public List<ulong> cantBeWarned = new List<ulong>();
        public List<ulong> channelsWithoutAutoMod = new List<ulong>();
        public List<string> allowedLinks = new List<string>();
        public List<ulong> allowedToLink = new List<ulong>();
        public List<string> badUEmojis = new List<string>();
        public List<ulong> ableToBan = new List<ulong>();
        public List<ulong> anouncementChannels = new List<ulong>();
        public TimeSpan? maxTempAction = null;
        public ulong mutedRole = 0;
        public ushort allowedCaps = 0;
        public bool useOwnerID = false;
        public bool moderateUsernames = false;
        public bool invitesAllowed = true;
        public uint? maxEmojis = null;
        public BsonDocument CatchAll { get; set; }
    }

    public class LogSettings : DataObject {
        [BsonId]
        public BsonString Id = "LogSettings";
        public ulong? pubLogChannel = null;
        public ulong logChannel;
        public bool logDeletes = true;
        public bool logEdits = false;
        public BsonDocument CatchAll { get; set; }
    }

    public class ReportSettings : DataObject {
        [BsonId]
        public BsonString Id = "ReportSettings";
        public TimeSpan? cooldown;
        public ulong? channelID;
        public ulong? requiredRole;
    }
}
