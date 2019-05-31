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
            string guildDir = guild.GetPath(createFile);
            ModerationSettings settings = null;

            ModerationFunctions.CheckDirectories(guild);

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;

            //The hope of these is for when Botcatmaxy starts to have an option to sync between servers
            if (guildDir != null && Directory.Exists(guildDir + "/" + guild.OwnerId)) {
                if (File.Exists(guildDir + "/moderationSettings.txt")) {
                    using (StreamReader sr = new StreamReader(guildDir + "/moderationSettings.txt"))
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
                File.Create(guildDir + "/moderationSettings.txt").Close();
                return new ModerationSettings();
            }

            return settings;
        }
        public static LogSettings LoadLogSettings(this IGuild guild, bool createFile = true) {
            string guildPath = guild.GetPath(createFile);
            LogSettings settings = null;

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;

            if (guild.GetPath(createFile) != null) {
                using (StreamReader sr = new StreamReader(@guildPath + "/logSettings.txt"))
                using (JsonTextReader reader = new JsonTextReader(sr)) {
                    settings = serializer.Deserialize<LogSettings>(reader);
                }
            
                if (!File.Exists(guildPath + "/logSettings.txt")) {
                    if (createFile) {
                        Console.WriteLine("Creating log settings");
                        SaveLogSettings(new LogSettings(), guild);
                        return new LogSettings();
                    }
                    return null;
                }
            }

            return settings;
        }
        public static List<BadWord> LoadBadWords(this IGuild Guild) {   
            string guildDir = Guild.GetPath(false);
            List<BadWord> badWords = null;

            if (guildDir == null || !File.Exists(guildDir + "/badwords.json")) {
                return null;
            }

            using (StreamReader sr = new StreamReader(@guildDir + "/badwords.json"))
            using (JsonTextReader reader = new JsonTextReader(sr)) {
                badWords = new JsonSerializer().Deserialize<List<BadWord>>(reader);
            }

            return badWords;
        }
        public static List<TempBan> LoadTempActions(this IGuild Guild, bool createNew = false) {
            List<TempBan> tempBans = new List<TempBan>();
            string guildDir = Guild.GetPath(createNew);
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

        public static List<Infraction> LoadInfractions(this SocketGuildUser user, string dir = "Discord", bool createDir = false) {
            List<Infraction> infractions = new List<Infraction>();

            if (Directory.Exists(user.Guild.GetPath(createDir) + "/Infractions/" + dir) && File.Exists(user.Guild.GetPath(createDir) + "/Infractions/" + dir + "/" + user.Id)) {
                BinaryFormatter newbf = new BinaryFormatter();
                FileStream newFile = File.Open(user.Guild.GetPath(createDir) + "/Infractions/" + dir + "/" + user.Id, FileMode.Open);
                Infraction[] oldInfractions;
                oldInfractions = (Infraction[])newbf.Deserialize(newFile);
                newFile.Close();
                foreach (Infraction infraction in oldInfractions) {
                    infractions.Add(infraction);
                }
            }
            return infractions;
        }

        public static string LoadMessage() {
            if (!File.Exists(Utilities.BasePath + "messages.json")) {
                File.Create(Utilities.BasePath + "messages.json").Close();

                JsonSerializer serializer = new JsonSerializer();
                using (StreamWriter sw = new StreamWriter(Utilities.BasePath + "messages.json"))
                using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                    serializer.Serialize(sw, "Empty message");
                }
            }
            string message;
            using (StreamReader sr = new StreamReader(Utilities.BasePath + "messages.json"))
            using (JsonTextReader reader = new JsonTextReader(sr)) {
                message = new JsonSerializer().Deserialize<string>(reader);
            }

            if (message == "Empty message") {
                return null;
            } else {
                return message;
            }
        }

        public static void SaveInfractions(this SocketGuildUser user, List<Infraction> infractions, string dir = "Discord") {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Create(user.Guild.GetPath(true) + "/Infractions/" + dir + "/" + user.Id);
            bf.Serialize(file, infractions.ToArray());
            file.Close();
        }

        public static void SaveTempBans(this List<TempBan> tempBans, IGuild Guild) {
            string guildDir = Guild.GetPath(true);
            tempBans.RemoveNullEntries();

            if (!File.Exists(guildDir + "/tempActions.json")) {
                File.Create(guildDir + "/tempActions.json").Close();
            }

            JsonSerializer serializer = new JsonSerializer();
            using (StreamWriter sw = new StreamWriter(@guildDir + "/tempActions.json"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, tempBans);
            }
        }

        public static void SaveBadWords(this List<BadWord> badWords, IGuild Guild) {
            if (!File.Exists(Guild.GetPath(true) + "/badwords.json")) {
                File.Create(Guild.GetPath(true) + "/badwords.json");
            }
            JsonSerializer serializer = new JsonSerializer();
            using (StreamWriter sw = new StreamWriter(@Guild.GetPath(true) + "/badwords.json"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, badWords);
            }
        }
        public static void SaveLogSettings(this LogSettings settings, IGuild Guild) {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;

            using (StreamWriter sw = new StreamWriter(Guild.GetPath(true) + "/logSettings.txt"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, settings);
            }
        }
        public static void SaveModSettings(this ModerationSettings settings, IGuild Guild) {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;

            using (StreamWriter sw = new StreamWriter(Guild.GetPath(true) + "/moderationSettings.txt"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, settings);
            }
        }
    }
}
