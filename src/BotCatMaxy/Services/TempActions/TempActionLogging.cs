using System;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Services.Logging;
using Discord;
using Discord.WebSocket;

namespace BotCatMaxy.Services.TempActions
{
    public static class TempActionLogging
    {
        public static async Task LogEndTempAct(this IGuild guild, UserRef user, string actType, string reason, TimeSpan length, bool manual = false)
        {
            var settings = guild.LoadFromFile<LogSettings>();
            var channel = await guild.GetTextChannelAsync(settings?.pubLogChannel ?? settings?.logChannel ?? 0);
            if (channel == null)
                return;

            var embed = new EmbedBuilder()
                        .AddField($"{user} has {(manual ? "manually " : string.Empty)}been un{actType}ed",
                            $"After {length.LimitedHumanize(2)}, because of {reason}")
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp();

            if (user.User != null)
                embed.WithAuthor(user.User);
            else
                embed.WithAuthor(user.ToString());
            await channel.SendMessageAsync(embed: embed.Build());
            
        }
    }
}