using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCatMaxy;
using BotCatMaxy.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Humanizer;
using Discord.Addons.Interactive;

namespace BotCatMaxy {
    //I want to move away from vague files like settings since conflicts are annoying
    public class SettingsModule : InteractiveBase<SocketCommandContext> {
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

        [Command("toggleserverstorage", RunMode = RunMode.Async)]
        [HasAdmin]
        public async Task ToggleServerIDUse() {
            await ReplyAsync("This is a legacy feature, if you want this done now contact blackcatmaxy@gmail.com with your guild invite and your username so I can get back to you");
        }

        [Command("allowwarn"), Alias("allowtowarn")]
        [RequireContext(ContextType.Guild)]
        [HasAdmin]
        public async Task AddWarnRole(SocketRole role) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (!settings.ableToWarn.Contains(role.Id)) {
                settings.ableToWarn.Add(role.Id);
            } else {
                _ = ReplyAsync("People with the role \"" + role.Name + "\" can already warn people");
            }

            settings.SaveToFile();

            await ReplyAsync("People with the role \"" + role.Name + "\" can now warn people");
        }

        [Command("setmaxpunishment"), Alias("setmaxpunish", "maxpunishmentset")]
        [RequireContext(ContextType.Guild), HasAdmin()]
        public async Task SetMaxPunishment(string length) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (length == "none") { //Maybe add more options that mean none
                if (settings.maxTempAction == null)
                    await ReplyAsync("The maximum temp punishment is already none");
                else {
                    settings.maxTempAction = null;
                    settings.SaveToFile();
                    await ReplyAsync("The maximum temp punishment is now none");
                }
                return;
            }
            TimeSpan? span = length.ToTime();
            if (span != null) {
                if (span == settings.maxTempAction) {
                    await ReplyAsync("The maximum temp punishment is already " + ((TimeSpan)span).LimitedHumanize(4));
                } else {
                    settings.maxTempAction = span;
                    settings.SaveToFile();
                    await ReplyAsync("The maximum temp punishment is now " + ((TimeSpan)span).LimitedHumanize(4));
                }
            } else {
                await ReplyAsync("Your time is incorrectly setup");
            }
        }

        [Command("setmutedrole"), Alias("mutedroleset")]
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

            settings.SaveToFile();

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
            settings.SaveToFile();

            _ = ReplyAsync("People with the role \"" + role.Name + "\" can now no longer warn people");
        }
    }

    [Group("logs")]
    [RequireContext(ContextType.Guild)]
    public class LogSettingCommands : ModuleBase<SocketCommandContext> {
        [Command("setchannel"), Alias("sethere")]
        [HasAdmin]
        public async Task SetLogChannel() {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>(true);

            if (settings == null) {
                await ReplyAsync("Settings is null");
                return;
            }

            if (Context.Client.GetChannel(settings.logChannel ?? 0) == Context.Channel) {
                await message.ModifyAsync(msg => msg.Content = "This channel already is the logging channel");
                return;
            } else {
                settings.logChannel = Context.Channel.Id;
            }

            settings.SaveToFile();
            await message.ModifyAsync(msg => msg.Content = "Set log channel to this channel");
        }

        [Command("setpubchannel"), Alias("setpublog", "publogset", "setpublogchannel")]
        [HasAdmin]
        public async Task SetPubLogChannel(string setNull = null) {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>(true);

            if (!setNull.IsNullOrEmpty() && (setNull.ToLower() == "none" || setNull.ToLower() == "null")) {
                settings.pubLogChannel = null;
                settings.SaveToFile();
                await message.ModifyAsync(msg => msg.Content = "Set public log channel to null");
                return;
            }
            if (Context.Client.GetChannel(settings.pubLogChannel ?? 0) == Context.Channel) {
                await message.ModifyAsync(msg => msg.Content = "This channel already is the logging channel");
                return;
            } else {
                settings.pubLogChannel = Context.Channel.Id;
            }

            settings.SaveToFile();
            await message.ModifyAsync(msg => msg.Content = "Set public log channel to this channel");
        }

        [Command("info"), Alias("settings")]
        public async Task DebugLogSettings() {
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>();

            if (settings == null) {
                await ReplyAsync("Settings is null");
                return;
            }

            var embed = new EmbedBuilder();

            SocketTextChannel logChannel = Context.Guild.GetTextChannel(settings.logChannel ?? 0);
            if (logChannel == null) {
                _ = ReplyAsync("Logging channel is null");
                return;
            }

            embed.AddField("Log channel", logChannel.Mention, true);
            embed.AddField("Log deleted messages", settings.logDeletes, true);
            if (settings.pubLogChannel != null) {
                var pubLogChannel = Context.Guild.GetTextChannel(settings.pubLogChannel.Value);
                if (pubLogChannel == null) embed.AddField("Public Log Channel", "Improper value set", true);
                else embed.AddField("Public Log Channel", pubLogChannel.Mention, true);
            }
            await ReplyAsync(embed: embed.Build());
        }

        [Command("toggleLogDeleted")]
        [HasAdmin]
        public async Task ToggleLoggingDeleted() {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = null;

            settings = Context.Guild.LoadFromFile<LogSettings>(true);

            settings.logDeletes = !settings.logDeletes;

            settings.SaveToFile();
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

            settings.SaveToFile();
            if (settings.logEdits) {
                await message.ModifyAsync(msg => msg.Content = "Edited messages will now be logged in the logging channel");
            } else {
                await message.ModifyAsync(msg => msg.Content = "Edited messages won't be logged now");
            }
        }

        [Command("setemergencychannel"), Alias("setbackupchannel", "backupset", "setbackup")]
        [HasAdmin]
        public async Task SetBackupLogChannel(string setNull = null) {
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>(true);

            if (!setNull.IsNullOrEmpty() && (setNull.ToLower() == "none" || setNull.ToLower() == "null")) {
                settings.backupChannel = null;
                settings.SaveToFile();
                await ReplyAsync("Set backup channel to null");
                return;
            }
            if (Context.Client.GetChannel(settings.backupChannel ?? 0) == Context.Channel) {
                await ReplyAsync("This channel already is the backup channel");
                return;
            } else {
                settings.backupChannel = Context.Channel.Id;
            }

            settings.SaveToFile();
            await ReplyAsync("Set backup channel to this channel");
        }
    }
}
