using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.WebSocket;
using Humanizer;
using System;
using System.Threading;
using System.Threading.Tasks;
using Discord.Addons.Hosting;
using Microsoft.Extensions.Logging;

namespace BotCatMaxy.Services.Logging;

public class LoggingHandler : DiscordClientService
{
    private readonly DiscordSocketClient _client;
    public LoggingHandler(DiscordSocketClient client, ILogger<DiscordClientService> logger) : base(client, logger)
    {
        _client = client;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.MessageDeleted += HandleDelete;
        _client.MessageUpdated += HandleEdit;
        _client.MessageReceived += HandleNew;

        LogSeverity.Info.Log("Logs", "Logging set up");
        return Task.CompletedTask;
    }

    public Task HandleNew(IMessage message)
    {
        Task.Run(() => LogNew(message));
        return Task.CompletedTask;
    }

    private Task HandleDelete(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        Task.Run(() => LogDelete(message, channel));
        return Task.CompletedTask;
    }

    private Task HandleEdit(Cacheable<IMessage, ulong> cachedMessage, SocketMessage newMessage, ISocketMessageChannel channel)
    {
        Task.Run(() => LogEdit(cachedMessage, newMessage, channel));
        return Task.CompletedTask;
    }

    public Task LogNew(IMessage message)
    {
        if (message.Channel as SocketGuildChannel != null && message.MentionedRoleIds != null && message.MentionedRoleIds.Count > 0)
        {
            SocketGuild guild = (message.Channel as SocketGuildChannel).Guild;
            // Check if we should stop doing the log based on settings
            LogSettings settings = guild.LoadFromFile<LogSettings>();

            if (settings.channelLogBlacklist.Contains(message.Channel.Id))
                return Task.CompletedTask;
            Task.Run(() => DiscordLogging.LogMessage("Role ping", message, guild, true));
        }
        return Task.CompletedTask;
    }

    public async Task LogEdit(Cacheable<IMessage, ulong> cachedMessage, SocketMessage newMessage, ISocketMessageChannel channel)
    {
        try
        {
            // Prevent from logging non-guild channels or invalid messages
            if (!(channel is SocketGuildChannel)) return;
            SocketGuild guild = (channel as SocketGuildChannel).Guild;
            IMessage oldMessage = await cachedMessage.GetOrDownloadAsync();
            if (oldMessage?.Content == newMessage.Content || newMessage.Author.IsBot || guild == null) return;
            // Check if we should stop doing the log based on settings
            LogSettings settings = guild.LoadFromFile<LogSettings>();
            if (settings?.logChannel == null || !settings.logEdits) return;
            if (settings.channelLogBlacklist.Contains(newMessage.Channel.Id)) 
                return;
            // Make sure we have a log channel set or abort
            SocketTextChannel logChannel = guild.GetChannel(settings.logChannel.Value) as SocketTextChannel;
            if (logChannel == null) return;

            var embed = new EmbedBuilder();
            if (string.IsNullOrEmpty(oldMessage?.Content))
            {
                embed.AddField($"Message was edited in #{newMessage.Channel.Name} from",
                    "`This message had no text or was null`");
            }
            else
            {
                embed.AddField($"Message was edited in #{newMessage.Channel.Name} from",
                    oldMessage.Content.Truncate(1020));
            }
            if (string.IsNullOrEmpty(newMessage.Content))
            {
                embed.AddField($"Message was edited in #{newMessage.Channel.Name} to",
                    "`This message had no text or is null`");
            }
            else
            {
                embed.AddField($"Message was edited in #{newMessage.Channel.Name} to",
                    newMessage.Content.Truncate(1020));
            }

            embed.AddField("Message Link", "[Click Here](" + newMessage.GetJumpUrl() + ")", false);
            embed.WithFooter("ID: " + newMessage.Id)
                 .WithAuthor(newMessage.Author)
                 .WithColor(Color.Teal)
                 .WithCurrentTimestamp();

            await logChannel.SendMessageAsync(embed: embed.Build());
        }
        catch (Exception exception)
        {
            await new LogMessage(LogSeverity.Error, "Logging", exception.Message, exception).Log();
        }
    }

    private async Task LogDelete(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> cachedChannel)
    {
        IMessageChannel channel = await cachedChannel.GetOrDownloadAsync();
        try
        {
            if (!(channel is SocketGuildChannel))
                return;
            SocketGuild guild = (channel as SocketGuildChannel).Guild;

            // Check if we should stop doing the log based on settings
            LogSettings settings = guild.LoadFromFile<LogSettings>();

            if (settings.channelLogBlacklist.Contains(channel.Id))
                return;
            await DiscordLogging.LogMessage("Deleted message", message.GetOrDownloadAsync().Result);
        }
        catch (Exception exception)
        {
            await new LogMessage(LogSeverity.Error, "Logging", "Error", exception).Log();
        }
        //Console.WriteLine(new LogMessage(LogSeverity.Info, "Logging", "Message deleted"));
    }
}