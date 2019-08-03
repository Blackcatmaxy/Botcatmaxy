using System;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using BotCatMaxy;
using BotCatMaxy.Settings;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace BotCatMaxy.Data {
    public static class SettingsData {
        public static T LoadFromFile<T>(this IGuild guild, string fileName, bool createFile = false) {
            Type typeParameterType = typeof(T);
            string guildDir = guild.GetPath(createFile);
            T settings = default(T);
            JsonSerializer serializer = new JsonSerializer();
            fileName = "/" + fileName;

            if (guildDir != null && Directory.Exists(guildDir)) {
                if (File.Exists(guildDir + fileName)) {
                    using (StreamReader sr = new StreamReader(guildDir + fileName))
                    using (JsonTextReader reader = new JsonTextReader(sr)) {
                        settings = serializer.Deserialize<T>(reader);
                    }
                } else if (createFile) {
                    object newSettings = (T)Activator.CreateInstance(typeof(T));
                    newSettings.SaveToFile(fileName, guild);
                    return (T)newSettings;
                }
            }

            return settings;
        }

        public static void SaveToFile(this object settings, string fileName, IGuild Guild) {
            JsonSerializer serializer = new JsonSerializer();
            fileName = "/" + fileName;

            using (StreamWriter sw = new StreamWriter(Guild.GetPath(true) + fileName))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, settings);
            }
        }

        public static List<Infraction> LoadInfractions(this SocketGuildUser user, string dir = "Discord", bool createDir = false) {
            List<Infraction> infractions = new List<Infraction>();
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
            }
            return infractions;
        }

        public static void SaveInfractions(this SocketGuildUser user, List<Infraction> infractions, string dir = "Discord") {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Create(user.Guild.GetPath(true) + "/Infractions/" + dir + "/" + user.Id);
            bf.Serialize(file, infractions.ToArray());
            file.Close();
        }
    }

    public class BadWords {
        public List<BadWord> all;
        public List<BadWord> onlyAlone;
        public List<BadWord> insideWords;

        public BadWords(IGuild guild) {
            all = guild.LoadFromFile<List<BadWord>>("badwords.json");         
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
