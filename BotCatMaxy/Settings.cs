using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BotCatMaxy;
using BotCatMaxy.Settings;

namespace BotCatMaxy {
    public class SettingsModule : ModuleBase<SocketCommandContext> {
        [Command("!commands")]
        public async Task ListCommands() {
            await ReplyAsync("View commands here https://docs.google.com/document/d/1uVYHX9WEe2aRy2QbzMIwHMHthxJsViqu5Ah-yFKCANc/edit?usp=sharing");
        }

        [Command("allowwarn")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task AddWarnRole(SocketRole role) {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do have administrator permissions");
                return;
            }

            ModerationSettings settings = SettingFunctions.LoadModSettings(Context.Guild, true);

            if (settings == null) {
                settings = new ModerationSettings();
            }
            if (!settings.ableToWarn.Contains(role.Id)) {
                settings.ableToWarn.Add(role.Id);
            } else {
                _ = ReplyAsync("People with the role \"" + role.Name + "\" can already warn people");
            }

            JsonSerializer serializer = new JsonSerializer();

            using (StreamWriter sw = new StreamWriter(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/moderationSettings.txt"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, settings);
            }

            _ = ReplyAsync("People with the role \"" + role.Name + "\" can now warn people");
        }

        [Command("removewarnability")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task RemoveWarnRole(SocketRole role) {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do have administrator permissions");
                return;
            }

            ModerationSettings settings = SettingFunctions.LoadModSettings(Context.Guild, true);

            if (settings == null) {
                settings = new ModerationSettings();
            }
            if (settings.ableToWarn.Contains(role.Id)) {
                settings.ableToWarn.Remove(role.Id);
            } else {
                _ = ReplyAsync("People with the role \"" + role.Name + "\" can't already warn people");
            }

            JsonSerializer serializer = new JsonSerializer();

            using (StreamWriter sw = new StreamWriter(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/moderationSettings.txt"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, settings);
            }

            _ = ReplyAsync("People with the role \"" + role.Name + "\" can now no longer warn people");
        }

        [Command("toggleinvitewarn")]
        [RequireContext(ContextType.Guild)]
        public async Task ToggleInviteWarn() {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do have administrator permissions");
                return;
            }

            Console.WriteLine(DateTime.Now.ToShortTimeString() + " Toggling invite warn");

            IUserMessage message = await ReplyAsync("Trying to toggle");
            ModerationFunctions.CheckDirectories(Context.Guild);
            ModerationSettings settings = null;

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;
            try {
                StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/moderationSettings.txt");
                JsonTextReader reader = new JsonTextReader(sr);
                settings = serializer.Deserialize<ModerationSettings>(reader);
            } catch (Exception error) {
                await message.ModifyAsync(msg => msg.Content = "an error has occurred loading settings " + error.Message);
                Console.WriteLine(DateTime.Now.ToShortTimeString() + "an error has occurred loading settings " + error.Message);
            }


            if (settings == null) {
                settings = new ModerationSettings();
                Console.WriteLine(DateTime.Now.ToShortTimeString() + " Creating new mod settings");
            }
            settings.invitesAllowed = !settings.invitesAllowed;
            Console.WriteLine(DateTime.Now.ToShortTimeString() + " setting invites to " + settings.invitesAllowed);

            /*using (StreamWriter file = File.CreateText(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/moderationSettings.text")) {
                JsonSerializer newserializer = new JsonSerializer();
                newserializer.Serialize(file, settings);
            }*/

            using (StreamWriter sw = new StreamWriter(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/moderationSettings.txt"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, settings);
            }

            if (File.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/moderationSettings")) {
                Console.WriteLine(DateTime.Now.ToShortTimeString() + " mod settings saved");
            } else {
                Console.WriteLine(DateTime.Now.ToShortTimeString() + " mod settings not found after creation?!");
            }

            await message.ModifyAsync(msg => msg.Content = "set invites allowed to " + settings.invitesAllowed.ToString().ToLower());
        }

        [Command("badwords")]
        public async Task ListBadWords() {
            if (Directory.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId)) {
                JsonSerializer serializer = new JsonSerializer();
                serializer.NullValueHandling = NullValueHandling.Ignore;
                List<BadWord> badWords;

                using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/badwords.json"))
                using (JsonTextReader reader = new JsonTextReader(sr)) {
                    badWords = serializer.Deserialize<List<BadWord>>(reader);
                }
                IDMChannel dMChannel = await Context.User.GetOrCreateDMChannelAsync();
                if (dMChannel == null) {
                    await ReplyAsync("Something has gone wrong, ERROR: DMChannel is null");
                } else {
                    if (badWords == null) {
                        await dMChannel.SendMessageAsync("This server has no bad word filtering enabled");
                    }
                    string allBadWords = "";
                    foreach (BadWord badWord in badWords) {
                        if (allBadWords == "") {
                            allBadWords = "Any words that contain or are the following words are not allowed: " + badWord.euphemism;
                        } else {
                            if (badWord.euphemism != "") {
                                allBadWords += ", " + badWord.euphemism;
                            }
                        }
                    }
                    await dMChannel.SendMessageAsync(allBadWords);
                }
            }
        }

        [Command("explicitbadwords")]
        public async Task ListExplicitBadWords() {
            if (!((SocketGuildUser)Context.User).CanWarn()) {
                await ReplyAsync("You do not have permission to use this command");
                return;
            }
            if (Directory.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId)) {
                JsonSerializer serializer = new JsonSerializer();
                serializer.NullValueHandling = NullValueHandling.Ignore;
                List<BadWord> badWords;

                using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/badwords.json"))
                using (JsonTextReader reader = new JsonTextReader(sr)) {
                    badWords = serializer.Deserialize<List<BadWord>>(reader);
                }
                IDMChannel dMChannel = await Context.User.GetOrCreateDMChannelAsync();
                if (dMChannel == null) {
                    await ReplyAsync("Something has gone wrong, ERROR: DMChannel is null");
                } else {
                    if (badWords == null) {
                        await dMChannel.SendMessageAsync("This server has no bad word filtering enabled");
                    }
                    string allBadWords = "";
                    foreach (BadWord badWord in badWords) {
                        if (allBadWords == "") {
                            allBadWords = "Any words that contain or are the following words are not allowed: " + badWord.euphemism + (badWord.word);
                        } else {
                            allBadWords += ", " + badWord.euphemism + (badWord.word);
                        }
                    }
                    await dMChannel.SendMessageAsync(allBadWords);
                }
            }
        }

        [Command("addbadword")]
        [RequireContext(ContextType.Guild)]
        public async Task AddBadWord(string word, string euphemism = null, float size = 0.5f) {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do have administrator permissions");
                return;
            }
            ModerationFunctions.CheckDirectories(Context.Guild);
            BadWord badWord = new BadWord();
            badWord.word = word;
            badWord.euphemism = euphemism;
            badWord.size = size;
            List<BadWord> badWords;

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;

            /*if (!File.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/badwords.json")) {
                File.Create("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/badwords.json");
            }*/

            using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/badwords.json"))
            using (JsonTextReader reader = new JsonTextReader(sr)) {
                badWords = serializer.Deserialize<List<BadWord>>(reader);
            }

            if (badWords == null) {
                badWords = new List<BadWord>();
            }
            badWords.Add(badWord);

            using (StreamWriter sw = new StreamWriter(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/badwords.json"))
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                serializer.Serialize(sw, badWords);
            }

            if (euphemism != null) {
                await ReplyAsync("added " + badWord.word + " also known as " + euphemism + " to bad word list");
            } else {
                await ReplyAsync("added " + badWord.word + " to bad word list");
            }
        }

        [Command("removebadword")]
        [RequireContext(ContextType.Guild)]
        public async Task RemoveBadWord(string word) {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do have administrator permissions");
                return;
            }

            ModerationFunctions.CheckDirectories(Context.Guild);
            List<BadWord> badWords; 

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;

            if (!File.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/badwords.json")) {
                await ReplyAsync("No bad words file found");
                return;
            }

            using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/badwords.json"))
            using (JsonTextReader reader = new JsonTextReader(sr)) {
                badWords = serializer.Deserialize<List<BadWord>>(reader);
            }

            if (badWords == null) {
                await ReplyAsync("Bad words is null");
                return;
            }
            BadWord badToRemove = null;
            foreach (BadWord badWord in badWords) {
                if (badWord.word == word) {
                    badToRemove = badWord;
                }
            }
            if (badToRemove != null) {
                badWords.Remove(badToRemove);
                using (StreamWriter sw = new StreamWriter(@"/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/badwords.json"))
                using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                    serializer.Serialize(sw, badWords);
                }

                await ReplyAsync("removed " + word + " from bad word list");
            } else {
                await ReplyAsync("Bad word list doesn't contain " + word);
            }
        }
    }

    [Group("logs")]
    [RequireContext(ContextType.Guild)]
    public class LogSettingCommands : ModuleBase<SocketCommandContext> {
        [Command("setchannel")]
        public async Task SetLogChannel() {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do not have administrator access");
                return;
            }
            IUserMessage message = await ReplyAsync("Setting...");
            ModerationFunctions.CheckDirectories(Context.Guild);
            LogSettings settings = null;

            settings = SettingFunctions.LoadLogSettings(Context.Guild, true);

            if (Context.Client.GetChannel(settings.logChannel) == Context.Channel) {
                await ReplyAsync("This channel already is the logging channel");
                return;
            } else {
                settings.logChannel = Context.Channel.Id;
            }

            settings.SaveLogSettings(Context.Guild);
            await message.ModifyAsync(msg => msg.Content = "Set log channel");
        }

        [Command("info")]
        public async Task DebugLogSettings() {
            LogSettings settings = SettingFunctions.LoadLogSettings(Context.Guild, false);

            var embed = new EmbedBuilder();
           
            SocketTextChannel logChannel = Context.Guild.GetTextChannel(settings.logChannel);
            if (logChannel == null) {
                _ = ReplyAsync("Logging channel is null");
                return;
            }

            embed.AddField("Log channel", logChannel, true);
            embed.AddField("Log deleted messages", settings.logDeletes, true);
            await ReplyAsync(embed: embed.Build());
        }

        [Command("toggleLogDeleted")]
        public async Task ToggleLoggingDeleted() {
            if (!Utilities.HasAdmin(Context.Message.Author as SocketGuildUser)) {
                await ReplyAsync("You do not have administrator access");
                return;
            }
            IUserMessage message = await ReplyAsync("Setting...");
            ModerationFunctions.CheckDirectories(Context.Guild);
            LogSettings settings = null;

            settings = SettingFunctions.LoadLogSettings(Context.Guild, true);

            settings.logDeletes = !settings.logDeletes;

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;

            settings.SaveLogSettings(Context.Guild);
            if (settings.logDeletes) {
                await message.ModifyAsync(msg => msg.Content = "Deleted messages will now be logged in the logging channel");
            } else {
                await message.ModifyAsync(msg => msg.Content = "Deleted messages won't be logged now");
            }
        }
    }

    namespace Settings {
        public static class SettingFunctions {
            public static ModerationSettings LoadModSettings(IGuild guild, bool createFile = true) {
                ModerationSettings settings = null;

                ModerationFunctions.CheckDirectories(guild);

                JsonSerializer serializer = new JsonSerializer();
                serializer.NullValueHandling = NullValueHandling.Include;

                //The hope of these is for when Botcatmaxy starts to have an option to sync between servers
                if (Directory.Exists("/home/bob_the_daniel/Data/" + guild)) {
                    using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + guild + "/moderationSettings.txt"))
                    using (JsonTextReader reader = new JsonTextReader(sr)) {
                        settings = serializer.Deserialize<ModerationSettings>(reader);
                    }
                } else if (Directory.Exists("/home/bob_the_daniel/Data/" + guild.OwnerId)) {
                    using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + guild.OwnerId + "/moderationSettings.txt"))
                    using (JsonTextReader reader = new JsonTextReader(sr)) {
                        settings = serializer.Deserialize<ModerationSettings>(reader);
                    }
                }

                if (createFile && settings == null) {
                    File.Create("/home/bob_the_daniel/Data/" + guild.OwnerId + "/moderationSettings.txt");
                    return new ModerationSettings();
                }

                return settings;
            }
            public static LogSettings LoadLogSettings(IGuild guild, bool createFile = true) {
                LogSettings settings = null;

                try {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.NullValueHandling = NullValueHandling.Include;

                    if (Directory.Exists("/home/bob_the_daniel/Data/" + guild)) {
                        using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + guild + "/logSettings.txt"))
                        using (JsonTextReader reader = new JsonTextReader(sr)) {
                            settings = serializer.Deserialize<LogSettings>(reader);
                        }
                    } else if (Directory.Exists("/home/bob_the_daniel/Data/" + guild.OwnerId)) {
                        using (StreamReader sr = new StreamReader(@"/home/bob_the_daniel/Data/" + guild.OwnerId + "/logSettings.txt"))
                        using (JsonTextReader reader = new JsonTextReader(sr)) {
                            settings = serializer.Deserialize<LogSettings>(reader);
                        }
                    }
                } catch (Exception error) {
                    Console.WriteLine(DateTime.Now.ToShortTimeString() + " an error has occurred loading settings: " + error.Message);
                }

                if (createFile && settings == null) {
                    File.Create("/home/bob_the_daniel/Data/" + guild.OwnerId + "/logSettings.txt");
                    return new LogSettings();
                }

                return settings;
            }

            public static void SaveLogSettings(this LogSettings settings, SocketGuild Guild) {
                JsonSerializer serializer = new JsonSerializer();
                serializer.NullValueHandling = NullValueHandling.Include;

                using (StreamWriter sw = new StreamWriter(@"/home/bob_the_daniel/Data/" + Guild.OwnerId + "/logSettings.txt"))
                using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                    serializer.Serialize(sw, settings);
                }
            }
        }
        //Might replace these with a struct OR make them inherit from a "Saveable" class or make an interface
        //so then we can have a dynamic function to save things?
        public class BadWord {
            public string word;
            public string euphemism;
            public float size;
        }
        public class ModerationSettings {
            public List<ulong> ableToWarn = new List<ulong>();
            public List<ulong> ableToBan = new List<ulong>();
            public bool useOwnerID = false;
            public bool moderateUsernames = false;
            public bool invitesAllowed = true;
        }

        public class LogSettings {
            public ulong logChannel;
            public bool logDeletes = true;
            public bool logEdits = false;
        }
    }
}
