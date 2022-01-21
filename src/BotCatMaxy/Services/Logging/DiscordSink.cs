using System;
using Discord;
using Discord.WebSocket;
using Humanizer;
using Serilog;
using Serilog.Core;
using Serilog.Events;

#nullable enable
namespace BotCatMaxy.Services.Logging;

public class DiscordSink : ILogEventSink
{
    private const string _nullIndicator = "NULL_MESSAGE";
    public static DiscordSocketClient? Client;
    private ITextChannel? _channel;

    public void Emit(LogEvent logEvent)
    {
        try
        {
            _channel ??= BotInfo.LogChannel;

            if (_channel == null)
                return;

            var embed = RenderEmbed(logEvent);
            _ = _channel.SendMessageAsync(embed: embed.Build());
        }
        catch (Exception e)
        {
            Log.Warning(e, "Something went wrong logging to Discord");
        }
    }

    public EmbedBuilder RenderEmbed(LogEvent logEvent)
    {
        var title = $"{logEvent.Level} message";
        if (logEvent.Properties.TryGetValue("Source", out var sourceValue))
        {
            title += $" from {sourceValue.ToString()}";
        }

        var embed = new EmbedBuilder()
                    .WithAuthor(BotInfo.User)
                    .AddField(title + ':', logEvent.RenderMessage().Truncate(2048))
                    .WithCurrentTimestamp();

        //Get UserID if possible and add field
        if (logEvent.Properties.TryGetValue("UserId", out var userIdValue))
        {
            var user = Client!.GetUser(ulong.Parse(userIdValue.ToString()));
            embed.AddField("Executor", $"{user.Username}#{user.Discriminator}\n({user.Id})", true);
        }

        SocketGuild? guild = null;
        ISocketMessageChannel? channel = null;
        //Get Channel if exists and add field
        if (logEvent.Properties.TryGetValue("ChannelId", out var channelIdValue))
        {
            channel = Client!.GetChannel(ulong.Parse(channelIdValue.ToString())) as ISocketMessageChannel;
            if (channel is SocketTextChannel guildChannel)
                guild = guildChannel.Guild;
            string channelValue = guild is null ? "Direct Messages" : $"{channel!.Name}\n({channel.Id})";
            embed.AddField("Channel:", channelValue, true);
        }

        //Get GuildID if not from channel and then add field
        if (guild == null && logEvent.Properties.TryGetValue("GuildId", out var guildIdValue))
            guild = Client!.GetGuild(ulong.Parse(guildIdValue.ToString()));
        if (guild != null)
            embed.AddField("Guild:", $"{guild.Name}\n({guild.Id})", true);

        //Get Message info if exists and add field
        if (logEvent.Properties.TryGetValue("MessageId", out var messageIdValue))
        {
            var messageIdString = messageIdValue.ToString();
            var message = channel?.GetCachedMessage(ulong.Parse(messageIdString));
            var messageContent = message?.Content ?? logEvent.Properties["MessageContent"]?.ToString();

            string jumpLink = "";
            if (message != null)
                jumpLink = $"[Jump to Invocation]({message.GetJumpUrl()})";

            embed.AddField($"Message Content {messageIdString}",
                $"```{messageContent.Truncate(1000) ?? _nullIndicator}```{jumpLink}");
        }

        //Get Exception if exists and add field
        if (logEvent.Exception != null)
        {
            embed.AddField("Exception:", $"```{logEvent.Exception.ToString().Truncate(1016)}```");
        }

        return embed;
    }
}