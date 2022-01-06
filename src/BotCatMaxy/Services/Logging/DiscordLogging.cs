using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.WebSocket;
using Humanizer;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BotCatMaxy.Services.Logging;

public static class DiscordLogging
{
    public static volatile FixedSizedQueue<ulong> deletedMessagesCache = new(10);

    public static async Task<string> LogMessage(string reason, IMessage message, IGuild guild = null, bool addJumpLink = false, Color? color = null, IUser authorOveride = null, string textOverride = null)
    {
        try
        {
            if (message == null) return null;
            if (deletedMessagesCache.Any(delMsg => delMsg == message.Id)) return null;

            if (guild == null)
            {
                guild = (message.Channel as SocketGuildChannel).Guild;
                if (guild == null)
                {
                    return null;
                }
            }

            LogSettings settings = guild?.LoadFromFile<LogSettings>();
            var logChannel = await guild?.GetTextChannelAsync(settings?.logChannel ?? 0);
            if (settings == null || logChannel == null || !settings.logDeletes)
            {
                return null;
            }

            var embed = new EmbedBuilder();
            SocketTextChannel channel = message.Channel as SocketTextChannel;
            if (string.IsNullOrEmpty(message.Content))
                embed.AddField(reason + " in #" + message.Channel.Name,
                    "`This message had no text`", true);
            else
                embed.AddField(reason + " in #" + message.Channel.Name,
                    textOverride ?? message.Content.Truncate(1020), true);

            if (addJumpLink)
            {
                embed.AddField("Message Link", "[Click Here](" + message.GetJumpUrl() + ")", true);
            }

            string links = "";
            if (message.Attachments?.Count is not null or 0)
            {
                foreach (IAttachment attachment in message.Attachments)
                {
                    links += " " + attachment.ProxyUrl;
                }
            }

            embed.WithFooter($"Message ID:{message.Id} • User ID:{(authorOveride ?? message.Author).Id}")
                 .WithAuthor(authorOveride ?? message.Author)
                 .WithColor(color ?? Color.Blue)
                 .WithCurrentTimestamp();
            string link = logChannel.SendMessageAsync(embed: embed.Build()).Result.GetJumpUrl();
            if (!links.IsNullOrEmpty()) logChannel.SendMessageAsync("The message above had these attachments:" + links);
            return link;
        }
        catch (Exception exception)
        {
            _ = new LogMessage(LogSeverity.Error, "Logging", exception.Message, exception).Log();
        }
        return null;
    }

    public static async Task<IUserMessage> LogWarn(IGuild guild, IUser warner, ulong warneeID, string reason, string warnLink, string additionalPunishment = "")
    {
        try
        {
            LogSettings settings = guild.LoadFromFile<LogSettings>();
            ITextChannel channel = guild.GetTextChannelAsync(settings?.pubLogChannel ?? settings?.logChannel ?? 0).Result;
            if (channel == null) return null;

            var embed = new EmbedBuilder();
            embed.WithAuthor(warner);
            IGuildUser gWarnee = guild.GetUserAsync(warneeID).Result;
            if (gWarnee != null)
            {
                if (gWarnee.Nickname.IsNullOrEmpty())
                    embed.AddField($"{gWarnee.Username} ({gWarnee.Id}) has been {additionalPunishment}warned", "For " + reason);
                else
                    embed.AddField($"{gWarnee.Nickname} aka {gWarnee.Username} ({warneeID}) has been {additionalPunishment}warned", "For " + reason);
            }
            else
                embed.AddField($"({warneeID}) has been {additionalPunishment}warned", "For " + reason);

            if (!warnLink.IsNullOrEmpty()) embed.AddField("Jumplink", $"[Click Here]({warnLink})");
            embed.WithColor(Color.Gold);
            embed.WithCurrentTimestamp();

            return await channel.SendMessageAsync(embed: embed.Build());
        }
        catch (Exception e)
        {
            await new LogMessage(LogSeverity.Error, "Logging", "Error", e).Log();
        }
        return null;
    }

    public static void LogTempAct(IGuild guild, IUser warner, UserRef subject, string actType, string reason, string warnLink, TimeSpan length)
    {
        try
        {
            LogSettings settings = guild.LoadFromFile<LogSettings>();
            ITextChannel channel = guild.GetTextChannelAsync(settings?.pubLogChannel ?? settings?.logChannel ?? 0).Result;
            if (channel == null) return;

            var embed = new EmbedBuilder();
            embed.WithAuthor(warner);
            if (length == TimeSpan.Zero) //if not for forever
                embed.AddField($"{subject.Name(true, true)} has been perm {actType}ed", $"Because of {reason}");
            else
                embed.AddField($"{subject.Name(true, true)} has been temp-{actType}ed for {length.LimitedHumanize()}", $"Because of {reason}");
            if (!warnLink.IsNullOrEmpty()) embed.AddField("Jumplink", $"[Click Here]({warnLink})");
            embed.WithColor(Color.Red);
            embed.WithCurrentTimestamp();

            channel.SendMessageAsync(embed: embed.Build());
            return;
        }
        catch (Exception e)
        {
            _ = new LogMessage(LogSeverity.Error, "Logging", "Error", e).Log();
        }
        return;
    }
}