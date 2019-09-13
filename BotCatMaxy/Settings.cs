using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BotCatMaxy;
using BotCatMaxy.Data;
using BotCatMaxy.Settings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Humanizer;

namespace BotCatMaxy {
    public class SettingsModule : ModuleBase<SocketCommandContext> {
        public Task needsConfirmation;
        [Command("Settings Info")]
        [RequireContext(ContextType.Guild)]
        public async Task SettingsInfo() {
            var embed = new EmbedBuilder();
            var guildDir = Context.Guild.GetCollection(false);
            if (guildDir == null) {
                embed.AddField("Storage", "Not created yet", true);
            } else if (guildDir.CollectionNamespace.CollectionName == Context.Guild.OwnerId.ToString()) {
                embed.AddField("Storage", "Using Owner's ID", true);
            } else if (guildDir.CollectionNamespace.CollectionName == Context.Guild.Id.ToString()) {
                embed.AddField("Storage", "Using Guild ID", true);
            }

            await ReplyAsync(embed: embed.Build());
        }

        [Command("ToggleServerID")]
        [HasAdmin]
        public async Task ServerIDCommand() {
            await ReplyAsync("This will delete any existing data and changing back will also delete any existing data, use !confirm or !cancel to proceed");
            needsConfirmation = ToggleServerIDUse(Context.Guild);
        }

        [Command("confirm")]
        [HasAdmin]
        public async Task Confirm() {
            if (needsConfirmation == null) {
                await ReplyAsync("There is nothing to confirm");
            } else {
                needsConfirmation.Start();
                needsConfirmation = null;
            }
        }

        [Command("cancel")]
        [HasAdmin]
        public async Task Cancel() {
            if (needsConfirmation == null) {
                await ReplyAsync("There is nothing to cancel");
            } else {
                await ReplyAsync(needsConfirmation.ToString() + " event has been canceled");
                needsConfirmation = null;
            }
        }

        public async Task ToggleServerIDUse(SocketGuild guild) {
            if (Directory.Exists(Utilities.BasePath + guild.Id)) {
                await ReplyAsync("Switching to using owner ID to store data");
                Directory.Delete(Utilities.BasePath + guild.Id, true);
            } else {
                await ReplyAsync("Switching to using server ID to store data");
                Directory.CreateDirectory(Utilities.BasePath + guild.Id);
            }
        }

        [Command("allowwarn")]
        [RequireContext(ContextType.Guild)]
        [HasAdmin]
        public async Task AddWarnRole(SocketRole role) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (!settings.ableToWarn.Contains(role.Id)) {
                settings.ableToWarn.Add(role.Id);
            } else {
                _ = ReplyAsync("People with the role \"" + role.Name + "\" can already warn people");
            }

            settings.SaveToFile(Context.Guild);

            await ReplyAsync("People with the role \"" + role.Name + "\" can now warn people");
        }

        [Command("setmaxpunishment"), Alias("setmaxpunish")]
        [RequireContext(ContextType.Guild), HasAdmin()]
        public async Task SetMaxPunishment(string length) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (length == "none") { //Maybe add more options that mean none
                if (settings.maxTempAction == null) 
                    await ReplyAsync("The maximum temp punishment is already none");
                else {
                    settings.maxTempAction = null;
                    settings.SaveToFile(Context.Guild);
                    await ReplyAsync("The maximum temp punishment is now none");
                }
                return;
            }
            TimeSpan? span = length.ToTime();
            if (span != null) {
                if (span == settings.maxTempAction) {
                    await ReplyAsync("The maximum temp punishment is already " + ((TimeSpan)span).Humanize(4));
                } else {
                    settings.maxTempAction = span;
                    settings.SaveToFile(Context.Guild);
                    await ReplyAsync("The maximum temp punishment is now " + ((TimeSpan)span).Humanize(4));
                }
            } else {
                await ReplyAsync("Your time is incorrectly setup");
            }
        }

        [Command("setmutedrole")]
        [RequireContext(ContextType.Guild)]
        [HasAdmin]
        public async Task SetMutedRole(SocketRole role) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (settings == null) {
                settings = new ModerationSettings();
            }
            if (settings.mutedRole != role.Id) {
                settings.mutedRole = role.Id;
            } else {
                _ = ReplyAsync("The role \"" + role.Name + "\" is already the muted role");
                return;
            }

            settings.SaveToFile(Context.Guild);

            await ReplyAsync("The role \"" + role.Name + "\" is now the muted role");
        }

        [Command("removewarnability")]
        [RequireContext(ContextType.Guild)]
        [HasAdmin]
        public async Task RemoveWarnRole(SocketRole role) {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do have administrator permissions");
                return;
            }

            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (settings == null) {
                settings = new ModerationSettings();
            }
            if (settings.ableToWarn.Contains(role.Id)) {
                settings.ableToWarn.Remove(role.Id);
            } else {
                _ = ReplyAsync("People with the role \"" + role.Name + "\" can't already warn people");
            }
            settings.SaveToFile(Context.Guild);

            _ = ReplyAsync("People with the role \"" + role.Name + "\" can now no longer warn people");
        }
    }

    [Group("logs")]
    [RequireContext(ContextType.Guild)]
    public class LogSettingCommands : ModuleBase<SocketCommandContext> {
        [Command("setchannel")]
        [HasAdmin]
        public async Task SetLogChannel() {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>(true);

            if (settings == null) {
                await ReplyAsync("Settings is null");
                return;
            }

            if (Context.Client.GetChannel(settings.logChannel) == Context.Channel) {
                await ReplyAsync("This channel already is the logging channel");
                return;
            } else {
                settings.logChannel = Context.Channel.Id;
            }

            settings.SaveToFile(Context.Guild);
            await message.ModifyAsync(msg => msg.Content = "Set log channel to this channel");
        }

        [Command("setpubchannel")]
        [HasAdmin]
        public async Task SetPubLogChannel() {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>(true);

            if (Context.Client.GetChannel(settings.pubLogChannel ?? 0) == Context.Channel) {
                await ReplyAsync("This channel already is the logging channel");
                return;
            } else {
                settings.pubLogChannel = Context.Channel.Id;
            }

            settings.SaveToFile(Context.Guild);
            await message.ModifyAsync(msg => msg.Content = "Set log channel to this channel");
        }

        [Command("info")]
        public async Task DebugLogSettings() {
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>();

            if (settings == null) {
                await ReplyAsync("Settings is null");
                return;
            }

            var embed = new EmbedBuilder();

            SocketTextChannel logChannel = Context.Guild.GetTextChannel(settings.logChannel);
            if (logChannel == null) {
                _ = ReplyAsync("Logging channel is null");
                return;
            }

            embed.AddField("Log channel", logChannel.Mention, true);
            embed.AddField("Log deleted messages", settings.logDeletes, true);
            await ReplyAsync(embed: embed.Build());
        }

        [Command("toggleLogDeleted")]
        [HasAdmin]
        public async Task ToggleLoggingDeleted() {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = null;

            settings = Context.Guild.LoadFromFile<LogSettings>(true);

            settings.logDeletes = !settings.logDeletes;

            settings.SaveToFile(Context.Guild);
            if (settings.logDeletes) {
                await message.ModifyAsync(msg => msg.Content = "Deleted messages will now be logged in the logging channel");
            } else {
                await message.ModifyAsync(msg => msg.Content = "Deleted messages won't be logged now");
            }
        }

        [Command("toggleLogEdited")]
        [HasAdmin]
        public async Task ToggleLoggingEdited() {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = null;

            settings = Context.Guild.LoadFromFile<LogSettings>(true); 

            settings.logEdits = !settings.logEdits;

            settings.SaveToFile(Context.Guild);
            if (settings.logEdits) {
                await message.ModifyAsync(msg => msg.Content = "Edited messages will now be logged in the logging channel");
            } else {
                await message.ModifyAsync(msg => msg.Content = "Edited messages won't be logged now");
            }
        }
    }

    namespace Settings {
        public class TempAct {
            public TempAct(ulong userID, TimeSpan length, string reason) {
                user = userID;
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

        public class BadWord {
            public List<string> moreWords = new List<string>();
            public string word;
            public string euphemism;
            public float size;
            public bool partOfWord = true;
        }

        public class BadWordList {
            [BsonId]
            public string Id = "BadWordList";
            public List<BadWord> badWords = new List<BadWord>();
        }

        public class ModerationSettings {
            [BsonId]
            public string Id = "ModerationSettings";
            public List<ulong> ableToWarn = new List<ulong>();
            public List<ulong> cantBeWarned = new List<ulong>();
            public List<ulong> channelsWithoutAutoMod = new List<ulong>();
            public List<ulong> ableToBan = new List<ulong>();
            public List<string> allowedLinks = new List<string>();
            public List<ulong> allowedToLink = new List<ulong>();
            public TimeSpan? maxTempAction = null;
            public ulong mutedRole = 0;
            public ushort allowedCaps = 0;
            public bool useOwnerID = false;
            public bool moderateUsernames = false;
            public bool invitesAllowed = true;
            public BsonDocument CatchAll { get; set; }
        }

        public class LogSettings {
            [BsonId]
            public BsonString Id = "LogSettings";
            public ulong? pubLogChannel = null;
            public ulong logChannel;
            public bool logDeletes = true;
            public bool logEdits = false;
            public BsonDocument CatchAll { get; set; }
        }
    }
}
