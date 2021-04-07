using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace BotCatMaxy.Components.Logging
{
    [Name("Logging")]
    [Group("logs"), Alias("logs")]
    [Summary("Manages logging.")]
    [RequireContext(ContextType.Guild)]
    public class LoggingCommands : InteractiveModule
    {
        public LoggingCommands(IServiceProvider service) : base(service)
        {
        }

        [Command("setchannel"), Alias("sethere")]
        [Summary("Sets the logging channel to the current channel.")]
        [HasAdmin]
        public async Task SetLogChannel()
        {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>(true);

            if (settings == null)
            {
                await ReplyAsync("Settings is null");
                return;
            }

            if (Context.Client.GetChannelAsync(settings.logChannel ?? 0) == Context.Channel)
            {
                await message.ModifyAsync(msg => msg.Content = "This channel already is the logging channel");
                return;
            }
            else
            {
                settings.logChannel = Context.Channel.Id;
            }

            settings.SaveToFile();
            await message.ModifyAsync(msg => msg.Content = "Set log channel to this channel");
        }

        [Command("setpubchannel"), Alias("setpublog", "publogset", "setpublogchannel")]
        [Summary("Sets this channel as the public logging channel.")]
        [HasAdmin]
        public async Task SetPubLogChannel(string setNull = null)
        {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>(true);

            if (!setNull.IsNullOrEmpty() && (setNull.ToLower() == "none" || setNull.ToLower() == "null"))
            {
                settings.pubLogChannel = null;
                settings.SaveToFile();
                await message.ModifyAsync(msg => msg.Content = "Set public log channel to null");
                return;
            }
            if (Context.Client.GetChannelAsync(settings.pubLogChannel ?? 0) == Context.Channel)
            {
                await message.ModifyAsync(msg => msg.Content = "This channel already is the logging channel");
                return;
            }
            else
            {
                settings.pubLogChannel = Context.Channel.Id;
            }

            settings.SaveToFile();
            await message.ModifyAsync(msg => msg.Content = "Set public log channel to this channel");
        }

        [Command("info"), Alias("settings")]
        [Summary("Views logging settings.")]
        public async Task DebugLogSettings()
        {
            var socketContext = Context as SocketCommandContext; //Not ready for testing yet
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>();

            if (settings == null)
            {
                await ReplyAsync("Settings is null");
                return;
            }

            var embed = new EmbedBuilder();

            SocketTextChannel logChannel = socketContext.Guild.GetTextChannel(settings.logChannel ?? 0);
            if (logChannel == null)
            {
                _ = ReplyAsync("Logging channel is null");
                return;
            }

            embed.AddField("Log channel", logChannel.Mention, true);
            embed.AddField("Log deleted messages", settings.logDeletes, true);
            if (settings.pubLogChannel != null)
            {
                var pubLogChannel = socketContext.Guild.GetTextChannel(settings.pubLogChannel.Value);
                if (pubLogChannel == null) embed.AddField("Public Log Channel", "Improper value set", true);
                else embed.AddField("Public Log Channel", pubLogChannel.Mention, true);
            }
            await ReplyAsync(embed: embed.Build());
        }

        [Command("toggleLogDeleted")]
        [Summary("Toggles if deleted messages should be logged.")]
        [HasAdmin]
        public async Task ToggleLoggingDeleted()
        {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = null;

            settings = Context.Guild.LoadFromFile<LogSettings>(true);

            settings.logDeletes = !settings.logDeletes;

            settings.SaveToFile();
            if (settings.logDeletes)
            {
                await message.ModifyAsync(msg => msg.Content = "Deleted messages will now be logged in the logging channel");
            }
            else
            {
                await message.ModifyAsync(msg => msg.Content = "Deleted messages won't be logged now");
            }
        }

        [Command("toggleLogEdited")]
        [Summary("Toggles if edited messages should be logged.")]
        [HasAdmin]
        public async Task ToggleLoggingEdited()
        {
            IUserMessage message = await ReplyAsync("Setting...");
            LogSettings settings = null;

            settings = Context.Guild.LoadFromFile<LogSettings>(true);

            settings.logEdits = !settings.logEdits;

            settings.SaveToFile();
            if (settings.logEdits)
            {
                await message.ModifyAsync(msg => msg.Content = "Edited messages will now be logged in the logging channel");
            }
            else
            {
                await message.ModifyAsync(msg => msg.Content = "Edited messages won't be logged now");
            }
        }

        [Command("setemergencychannel"), Alias("setbackupchannel", "backupset", "setbackup")]
        [Summary("Sets this channel as the emergency channel.")]
        [HasAdmin]
        public async Task SetBackupLogChannel(string setNull = null)
        {
            LogSettings settings = Context.Guild.LoadFromFile<LogSettings>(true);

            if (!setNull.IsNullOrEmpty() && (setNull.ToLower() == "none" || setNull.ToLower() == "null"))
            {
                settings.backupChannel = null;
                settings.SaveToFile();
                await ReplyAsync("Set backup channel to null");
                return;
            }
            if (await Context.Client.GetChannelAsync(settings.backupChannel ?? 0) == Context.Channel)
            {
                await ReplyAsync("This channel already is the backup channel");
                return;
            }
            else
            {
                settings.backupChannel = Context.Channel.Id;
            }

            settings.SaveToFile();
            await ReplyAsync("Set backup channel to this channel");
        }
    }
}
