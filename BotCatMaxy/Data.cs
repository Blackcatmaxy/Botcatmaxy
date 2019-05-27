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
        public static ModerationSettings LoadModSettings(this SocketGuild guild, bool createFile = true) {
            ModerationSettings settings = null;

            ModerationFunctions.CheckDirectories(guild);

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;

            //The hope of these is for when Botcatmaxy starts to have an option to sync between servers
            if (Directory.Exists("/home/bob_the_daniel/Data/" + guild)) {
                Console.WriteLine("This should never happen");
                using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + guild + "/moderationSettings.txt"))
                using (JsonTextReader reader = new JsonTextReader(sr)) {
                    settings = serializer.Deserialize<ModerationSettings>(reader);
                }
            } else if (Directory.Exists("/home/bob_the_daniel/Data/" + guild.OwnerId)) {
                if (File.Exists("/home/bob_the_daniel/Data/" + guild.OwnerId + "/moderationSettings.txt")) {
                    using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + guild.OwnerId + "/moderationSettings.txt"))
                    using (JsonTextReader reader = new JsonTextReader(sr)) {
                        settings = serializer.Deserialize<ModerationSettings>(reader);
                    }
                } else {
                    Console.WriteLine("Creating mod settings");
                    SaveModSettings(new ModerationSettings(), guild);
                    return new ModerationSettings();
                }
            }

            if (createFile && settings == null) {
                File.Create("/home/bob_the_daniel/Data/" + guild.OwnerId + "/moderationSettings.txt").Close();
                return new ModerationSettings();
            }

            return settings;
        }
        public static LogSettings LoadLogSettings(this IGuild guild, bool createFile = true) {
            LogSettings settings = null;

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;

            //The hope of this is for when Botcatmaxy starts to have an option to sync between servers
            if (Directory.Exists("/home/bob_the_daniel/Data/" + guild.Id)) {
                Console.WriteLine("This should never happen");
                using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + guild.Id + "/logSettings.txt"))
                using (JsonTextReader reader = new JsonTextReader(sr)) {
                    settings = serializer.Deserialize<LogSettings>(reader);
                }//It should always go here \/
            } else if (Directory.Exists("/home/bob_the_daniel/Data/" + guild.OwnerId)) {
                if (!File.Exists(@"/home/bob_the_daniel/Data/" + guild.OwnerId + "/logSettings.txt")) {
                    if (createFile) {
                        Console.WriteLine("Creating log settings");
                        SaveLogSettings(new LogSettings(), guild);
                        return new LogSettings();
                    }
                    return null;
                } else {
                    Console.WriteLine("Loading log settings");
                    using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + guild.OwnerId + "/logSettings.txt"))
                    using (JsonTextReader reader = new JsonTextReader(sr)) {
                        settings = serializer.Deserialize<LogSettings>(reader);
                    }
                }
            }

            if (createFile && settings == null) {
                SaveLogSettings(new LogSettings(), guild);
                return new LogSettings();
            }

            return settings;
        }
        public static List<BadWord> LoadBadWords(this IGuild Guild) {
            List<BadWord> badWords = null;

            if (!File.Exists("/home/bob_the_daniel/Data/" + Guild.OwnerId + "/badwords.json")) {
                return null;
            }

            using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + Guild.OwnerId + "/badwords.json"))
            using (JsonTextReader reader = new JsonTextReader(sr)) {
                badWords = new JsonSerializer().Deserialize<List<BadWord>>(reader);
            }

            return badWords;
        }
        public static List<TempBan> LoadTempActions(this IGuild Guild, bool createNew = false) {
            List<TempBan> tempBans = new List<TempBan>();
            string guildDir = Guild.GuildDataPath(createNew);
            if (guildDir == null || !Directory.Exists(guildDir)) {
                return tempBans;
            }
            if (File.Exists(guildDir + "/tempActions.json")) {
                JsonSerializer serializer = new JsonSerializer();
                using (StreamWriter sw = new StreamWriter(@guildDir + "/tempActions.json"))
                using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                    serializer.Serialize(sw, tempBans);
                }
                if (tempBans == null) {
                    return new List<TempBan>();
                }
                return tempBans;
            } else {
                if (createNew) {
                    File.Create(guildDir + "/tempActions.json").Close();
                }
                return tempBans;
            }
        }

        public static List<Infraction> LoadInfractions(this SocketGuildUser user, string dir = "Discord") {
            List<Infraction> infractions = new List<Infraction>();

            if (Directory.Exists(user.Guild.GuildDataPath() + "/Infractions/" + dir) && File.Exists(user.Guild.GuildDataPath() + "/Infractions/" + dir + "/" + user.Id)) {
                BinaryFormatter newbf = new BinaryFormatter();
                FileStream newFile = File.Open(user.Guild.GuildDataPath() + "/Infractions/" + dir + "/" + user.Id, FileMode.Open);
                Infraction[] oldInfractions;
                oldInfractions = (Infraction[])newbf.Deserialize(newFile);
                newFile.Close();
                foreach (Infraction infraction in oldInfractions) {
                    infractions.Add(infraction);
                }
            }
            return infractions;
        }

        public static void SaveTempBans(this List<TempBan> tempBans, IGuild Guild) {
            string guildDir = Guild.GuildDataPath(true);
            tempBans.RemoveNullEntries();

            if (!File.Exists(guildDir + "/tempActions.json")) {
                File.Create(guildDir + "/tempActions.json");
            }

            JsonSerializer serializer = new JsonSerializer();
            using (StreamWriter sw = new StreamWriter(@guildDir + "/tempActions.json"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, tempBans);
            }
        }

        public static void SaveBadWords(this List<BadWord> badWords, IGuild Guild) {
            if (!File.Exists("/home/bob_the_daniel/Data/" + Guild.OwnerId + "/badwords.json")) {
                File.Create("/home/bob_the_daniel/Data/" + Guild.OwnerId + "/badwords.json");
            }
            JsonSerializer serializer = new JsonSerializer();
            using (StreamWriter sw = new StreamWriter(@"/home/bob_the_daniel/Data/" + Guild.OwnerId + "/badwords.json"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, badWords);
            }
        }
        public static void SaveLogSettings(this LogSettings settings, IGuild Guild) {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;

            using (StreamWriter sw = new StreamWriter(@"/home/bob_the_daniel/Data/" + Guild.OwnerId + "/logSettings.txt"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, settings);
            }
        }
        public static void SaveModSettings(this ModerationSettings settings, IGuild Guild) {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;

            using (StreamWriter sw = new StreamWriter(@"/home/bob_the_daniel/Data/" + Guild.OwnerId + "/moderationSettings.txt"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, settings);
            }
        }
    }
}
