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
        [Command("allowwarn")]
        [RequireContext(ContextType.Guild)]
        [HasAdmin]
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
        [HasAdmin]
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
        [HasAdmin]
        public async Task ToggleInviteWarn() {
            IUserMessage message = await ReplyAsync("Trying to toggle");
            ModerationFunctions.CheckDirectories(Context.Guild);
            ModerationSettings settings = Context.Guild.LoadModSettings(true);

            if (settings == null) {
                settings = new ModerationSettings();
                Console.WriteLine(DateTime.Now.ToShortTimeString() + " Creating new mod settings");
            }
            settings.invitesAllowed = !settings.invitesAllowed;
            Console.WriteLine(DateTime.Now.ToShortTimeString() + " setting invites to " + settings.invitesAllowed);

            settings.SaveModSettings(Context.Guild);

            if (File.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/moderationSettings")) {
                Console.WriteLine(DateTime.Now.ToShortTimeString() + " mod settings saved");
            } else {
                Console.WriteLine(DateTime.Now.ToShortTimeString() + " mod settings not found after creation?!");
            }

            await message.ModifyAsync(msg => msg.Content = "set invites allowed to " + settings.invitesAllowed.ToString().ToLower());
        }

        [Command("toggleautomod")]
        [HasAdmin]
        public async Task ToggleAutoMod() {
            ModerationSettings settings = Context.Guild.LoadModSettings(true);

            if (settings.channelsWithoutAutoMod.Contains(Context.Channel.Id)) {
                settings.channelsWithoutAutoMod.Remove(Context.Channel.Id);
                await ReplyAsync("Enabled automod in this channel");
            } else {
                settings.channelsWithoutAutoMod.Add(Context.Channel.Id);
                await ReplyAsync("Disabled automod in this channel");
            }

            settings.SaveModSettings(Context.Guild);
        }

        [Command("badwords")]
        public async Task ListBadWords() {
            if (Directory.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId)) {
                List<BadWord> badWords = Context.Guild.LoadBadWords();

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
        [CanWarn]
        public async Task ListExplicitBadWords() {
            if (Directory.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId)) {
                List<BadWord> badWords = Context.Guild.LoadBadWords();

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
                            allBadWords = "Any words that contain or are the following words are not allowed: " + badWord.euphemism + "(" + badWord.word + ")";
                        } else {
                            allBadWords += ", " + badWord.euphemism + "(" + badWord.word + ")";
                        }
                    }
                    await dMChannel.SendMessageAsync(allBadWords);
                }
            }
        }

        [Command("addbadword")]
        [HasAdmin]
        public async Task AddBadWord(string word, string euphemism = null, float size = 0.5f) {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do have administrator permission");
                return;
            }
            ModerationFunctions.CheckDirectories(Context.Guild);
            BadWord badWord = new BadWord {
                word = word,
                euphemism = euphemism,
                size = size
            };
            List<BadWord> badWords = Context.Guild.LoadBadWords();

            if (badWords == null) {
                badWords = new List<BadWord>();
            }
            badWords.Add(badWord);
            badWords.SaveBadWords(Context.Guild);

            if (euphemism != null) {
                await ReplyAsync("added " + badWord.word + " also known as " + euphemism + " to bad word list");
            } else {
                await ReplyAsync("added " + badWord.word + " to bad word list");
            }
        }

        [Command("removebadword")]
        [HasAdmin]
        public async Task RemoveBadWord(string word) {
            ModerationFunctions.CheckDirectories(Context.Guild);
            List<BadWord> badWords = Context.Guild.LoadBadWords(); ;

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
                badWords.SaveBadWords(Context.Guild);

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
        [HasAdmin]
        public async Task SetLogChannel() {
            IUserMessage message = await ReplyAsync("Setting...");
            ModerationFunctions.CheckDirectories(Context.Guild);
            LogSettings settings = null;

            settings = SettingFunctions.LoadLogSettings(Context.Guild, true);

            if (settings != null && Context.Client.GetChannel(settings.logChannel) == Context.Channel) {
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
        [HasAdmin]
        public async Task ToggleLoggingDeleted() {
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
            public static ModerationSettings LoadModSettings(this IGuild guild, bool createFile = true) {
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
                    var newFile = File.Create("/home/bob_the_daniel/Data/" + guild.OwnerId + "/moderationSettings.txt");
                    newFile.Close();
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
                if (File.Exists(Guild.GuildDataPath() + "tempActions.json")) {
                    JsonSerializer serializer = new JsonSerializer();
                    using (StreamWriter sw = new StreamWriter(@guildDir + "tempActions.json"))
                    using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                        serializer.Serialize(sw, tempBans);
                    }
                    return tempBans;
                } else {
                    if (createNew) {
                        File.Create(guildDir + "tempActions.json");
                    }
                    return tempBans;
                }
            }
            public static void SaveTempBans(this List<TempBan> tempBans, IGuild Guild) {
                string guildDir = Guild.GuildDataPath(true);
                tempBans.RemoveNullEntries();

                if (!File.Exists(guildDir + "tempActions.json")) {
                    File.Create(guildDir + "tempActions.json");
                }

                JsonSerializer serializer = new JsonSerializer();
                using (StreamWriter sw = new StreamWriter(@guildDir + "tempActions.json"))
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
        //Might replace these with a struct OR make them inherit from a "Saveable" class or make an interface
        //so then we can have a dynamic function to save things?
        public class TempBan {
            public TempBan(ulong userID, int days) {
                personBanned = userID;
                length = days;
                dateBanned = DateTime.Now;
            }
            public ulong personBanned;
            public int length;
            public DateTime dateBanned;
        }
        public class BadWord {
            public string word;
            public string euphemism;
            public float size;
        }
        public class ModerationSettings {
            public List<ulong> ableToWarn = new List<ulong>();
            public List<ulong> cantBeWarned = new List<ulong>();
            public List<ulong> channelsWithoutAutoMod = new List<ulong>();
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
