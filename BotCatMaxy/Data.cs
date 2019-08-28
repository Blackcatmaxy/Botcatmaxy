using System;
using Discord;
using Discord.WebSocket;
using BotCatMaxy;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using BotCatMaxy.Settings;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace BotCatMaxy.Data {
    public static class SettingsData {
        public static T LoadFromFile<T>(this IGuild guild, bool createFile = false) {
            var file = default(T);
            var collection = guild.GetCollection(createFile);
            
            if (collection != null) {
                //var newFileDoc = Activator.CreateInstance(typeof(T)).ToBsonDocument();
                var filter = Builders<BsonDocument>.Filter.Eq("_id", typeof(T).Name);
                using (var cursor = collection.Find(filter).ToCursor()) {
                    var doc = cursor.FirstOrDefault();
                    //var options = new BsonTypeMapperOptions { MapBsonDocumentTo = typeof(T) };
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

            /*using (StreamWriter sw = new StreamWriter(Guild.GetPath(true) + fileName))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, settings);
            }*/
        }

        public static List<Infraction> LoadInfractions(this SocketGuildUser user, string dir = "Discord", bool createDir = false) {
            /*List<Infraction> infractions = new List<Infraction>();
            string guildDir = user.Guild.GetPath(createDir);

            if (Directory.Exists(guildDir + "/Infractions/" + dir) && File.Exists(guildDir + "/Infractions/" + dir + "/" + user.Id)) {
                BinaryFormatter newbf = new BinaryFormatter();
                FileStream newFile = File.Open(guildDir + "/Infractions/" + dir + "/" + user.Id, FileMode.Open);
                Infraction[] oldInfractions;
                oldInfractions = (Infraction[])newbf.Deserialize(newFile);
                newFile.Close();
                foreach (Infraction infraction in oldInfractions) {
                    infractions.Add(infraction);
                }
            }*/
            return null;
        }

        public static void SaveInfractions(this SocketGuildUser user, List<Infraction> infractions, string dir = "Discord") {
            /*BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Create(user.Guild.GetPath(true) + "/Infractions/" + dir + "/" + user.Id);
            bf.Serialize(file, infractions.ToArray());
            file.Close();*/
        }
    }

    public class BadWords {
        public List<BadWord> all;
        public List<BadWord> onlyAlone;
        public List<BadWord> insideWords;

        public BadWords(IGuild guild) {
            all = guild.LoadFromFile<BadWordList>().badWords ?? new List<BadWord>();
            onlyAlone = new List<BadWord>();
            insideWords = new List<BadWord>();

            if (all == null) {
                return;
            }
            foreach (BadWord badWord in all) {
                if (badWord.partOfWord) insideWords.Add(badWord);
                else onlyAlone.Add(badWord); 
            }
        }
    }
}
