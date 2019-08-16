using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BotCatMaxy;
using BotCatMaxy.Data;
using BotCatMaxy.Settings;

namespace BotCatMaxy {
    public class SettingsModule : ModuleBase<SocketCommandContext> {
        public Task needsConfirmation;
        [Command("Settings Info")]
        [RequireContext(ContextType.Guild)]
        public async Task SettingsInfo() {
            var embed = new EmbedBuilder();
            string guildDir = Context.Guild.GetPath(false);
            if (guildDir == null) {
                embed.AddField("Storage", "Not created yet", true);
            } else if (guildDir.Contains(Context.Guild.OwnerId.ToString())) {
                embed.AddField("Storage", "Using Owner's ID", true);
            } else if (guildDir.Contains(Context.Guild.Id.ToString())) {
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
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>("moderationSettings.txt", true);

            if (settings == null) {
                settings = new ModerationSettings();
            }
            if (!settings.ableToWarn.Contains(role.Id)) {
                settings.ableToWarn.Add(role.Id);
            } else {
                _ = ReplyAsync("People with the role \"" + role.Name + "\" can already warn people");
            }

            settings.SaveToFile("moderationSettings.txt", Context.Guild);

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

            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>("moderationSettings.txt", true);

            if (settings == null) {
                settings = new ModerationSettings();
            }
            if (settings.ableToWarn.Contains(role.Id)) {
                settings.ableToWarn.Remove(role.Id);
            } else {
                _ = ReplyAsync("People with the role \"" + role.Name + "\" can't already warn people");
            }

            JsonSerializer serializer = new JsonSerializer();

            settings.SaveToFile("moderationSettings.txt", Context.Guild);

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
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>("logSettings.txt", true);

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

            settings.SaveToFile("logSettings.txt", Context.Guild);
            await message.ModifyAsync(msg => msg.Content = "Set log channel");
        }

        [Command("info")]
        public async Task DebugLogSettings() {
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>("logSettings.txt");

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

            settings = Context.Guild.LoadFromFile<LogSettings>("logSettings.txt", true);

            settings.logDeletes = !settings.logDeletes;

            settings.SaveToFile("logSettings.txt", Context.Guild);
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

            settings = Context.Guild.LoadFromFile<LogSettings>("logSettings.txt", true); 

            settings.logEdits = !settings.logEdits;

            settings.SaveToFile("logSettings.txt", Context.Guild);
            if (settings.logEdits) {
                await message.ModifyAsync(msg => msg.Content = "Edited messages will now be logged in the logging channel");
            } else {
                await message.ModifyAsync(msg => msg.Content = "Edited messages won't be logged now");
            }
        }
    }

    namespace Settings {
        //Might replace these with a struct OR make them inherit from a "Saveable" class or make an interface
        //so then we can have a dynamic function to save things?
        public class TempBan {
            public TempBan(ulong userID, DateTime length, string reason) {
                user = userID;
                this.reason = reason;
                dateBanned = DateTime.Now;
                timeUnbanned = length;
            }
            public string reason;
            public ulong user;
            public DateTime timeUnbanned;
            public DateTime dateBanned;
        }
        public class BadWord {
            public string word;
            public string euphemism;
            public float size;
            public bool partOfWord = true;
        }
        public class ModerationSettings {
            public List<ulong> ableToWarn = new List<ulong>();
            public List<ulong> cantBeWarned = new List<ulong>();
            public List<ulong> channelsWithoutAutoMod = new List<ulong>();
            public List<ulong> ableToBan = new List<ulong>();
            public List<string> allowedLinks = new List<string>();
            public List<ulong> allowedToLink = new List<ulong>();
            public ushort allowedCaps = 0;
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
