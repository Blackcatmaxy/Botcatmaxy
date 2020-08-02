using BotCatMaxy;
using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace BotCatMaxy.Moderation
{
    public struct WarnResult
    {
        public readonly bool success;
        public readonly string description;
        public readonly int warnsAmount;

        public WarnResult(int warnsAmount)
        {
            success = true;
            description = null;
            this.warnsAmount = warnsAmount;
        }

        public WarnResult(string error)
        {
            success = false;
            description = error;
            warnsAmount = -1; //to denote error
        }
    }

    public static class PunishFunctions
    {
        public static async Task<WarnResult> Warn(this UserRef userRef, float size, string reason, SocketTextChannel channel, string logLink = null)
        {
            Contract.Requires(userRef != null);
            if (userRef.gUser != null)
                return await userRef.gUser.Warn(size, reason, channel, logLink);
            else
                return await userRef.ID.Warn(size, reason, channel, userRef.user, logLink);
        }

        public static async Task<WarnResult> Warn(this SocketGuildUser user, float size, string reason, SocketTextChannel channel, string logLink = null)
        {
            try
            {
                if (user.CantBeWarned())
                {
                    return new WarnResult("This person can't be warned");
                }

                return await user.Id.Warn(size, reason, channel, user, logLink);
            }
            catch (Exception e)
            {
                await new LogMessage(LogSeverity.Error, "Warn", "An exception has happened while warning", e).Log();
                return new WarnResult(e.ToString());
            }
        }

        public static async Task<WarnResult> Warn(this ulong userID, float size, string reason, SocketTextChannel channel, IUser warnee = null, string logLink = null)
        {
            if (size > 999 || size < 0.01)
            {
                return new WarnResult("Why would you need to warn someone with that size?");
            }

            List<Infraction> infractions = userID.AddWarn(size, reason, channel.Guild, logLink);

            try
            {
                if (warnee != null)
                {
                    LogSettings logSettings = channel.Guild.LoadFromFile<LogSettings>(false);
                    IUser[] users = null;
                    if (logSettings?.pubLogChannel != null && channel.Guild.TryGetChannel(logSettings.pubLogChannel.Value, out IGuildChannel logChannel))
                        users = await (logChannel as ISocketMessageChannel).GetUsersAsync().Flatten().ToArrayAsync();
                    else
                        users = await (channel as ISocketMessageChannel).GetUsersAsync().Flatten().ToArrayAsync();
                    if (!users.Any(xUser => xUser.Id == userID))
                    {
                        warnee.TryNotify($"You have been warned in {channel.Guild.Name} discord for \"{reason}\" in a channel you can't view");
                    }
                }
            }
            catch { }
            return new WarnResult(infractions.Count);
        }

        public static List<Infraction> AddWarn(this ulong userID, float size, string reason, IGuild guild, string logLink)
        {
            List<Infraction> infractions = userID.LoadInfractions(guild, true);
            Infraction newInfraction = new Infraction
            {
                reason = reason,
                time = DateTime.UtcNow,
                size = size
            };
            if (!string.IsNullOrEmpty(logLink)) newInfraction.logLink = logLink;
            infractions.Add(newInfraction);
            userID.SaveInfractions(guild, infractions);
            return infractions;
        }

        public struct InfractionsInDays
        {
            public float sum;
            public int count;
        }

        public struct InfractionInfo
        {
            public InfractionsInDays infractionsToday;
            public InfractionsInDays infractions30Days;
            public InfractionsInDays totalInfractions;
            public InfractionsInDays infractions7Days;
            public List<string> infractionStrings;
            public InfractionInfo(List<Infraction> infractions, int amount = 5, bool showLinks = false)
            {
                infractionsToday = new InfractionsInDays();
                infractions30Days = new InfractionsInDays();
                totalInfractions = new InfractionsInDays();
                infractions7Days = new InfractionsInDays();
                infractionStrings = new List<string> { "" };

                infractions.Reverse();
                if (infractions.Count < amount)
                {
                    amount = infractions.Count;
                }
                int n = 0;
                for (int i = 0; i < infractions.Count; i++)
                {
                    Infraction infraction = infractions[i];

                    //Gets how long ago all the infractions were
                    TimeSpan dateAgo = DateTime.UtcNow.Subtract(infraction.time);
                    totalInfractions.sum += infraction.size;
                    totalInfractions.count++;
                    if (dateAgo.Days <= 7)
                    {
                        infractions7Days.sum += infraction.size;
                        infractions7Days.count++;
                    }
                    if (dateAgo.Days <= 30)
                    {
                        infractions30Days.sum += infraction.size;
                        infractions30Days.count++;
                        if (dateAgo.Days < 1)
                        {
                            infractionsToday.sum += infraction.size;
                            infractionsToday.count++;
                        }
                    }

                    string size = "";
                    if (infraction.size != 1)
                    {
                        size = "(" + infraction.size + "x) ";
                    }

                    if (n < amount)
                    {
                        string jumpLink = "";
                        string timeAgo = dateAgo.LimitedHumanize(2);
                        if (showLinks && !infraction.logLink.IsNullOrEmpty()) jumpLink = $" [[Logged Here]({infraction.logLink})]";
                        string infracInfo = $"[{MathF.Abs(i - infractions.Count)}] {size}{infraction.reason}{jumpLink} - {timeAgo}";
                        n++;

                        if ((infractionStrings.LastOrDefault() + infracInfo).Length < 1024)
                        {
                            if (infractionStrings.LastOrDefault().NotEmpty()) infractionStrings[infractionStrings.Count - 1] += "\n";
                            infractionStrings[infractionStrings.Count - 1] += infracInfo;
                        }
                        else
                        {
                            infractionStrings.Add(infracInfo);
                        }
                    }
                }
            }
        }

        public static Embed GetEmbed(this List<Infraction> infractions, UserRef userRef, int amount = 5, bool showLinks = false)
        {
            InfractionInfo data = new InfractionInfo(infractions, amount, showLinks);

            //Builds infraction embed
            var embed = new EmbedBuilder();
            embed.AddField("Today",
                $"{data.infractionsToday.sum} sum**|**{data.infractionsToday.count} count", true);
            embed.AddField("Last 7 days",
                $"{data.infractions7Days.sum} sum**|**{data.infractions7Days.count} count", true);
            embed.AddField("Last 30 days",
                $"{data.infractions30Days.sum} sum**|**{data.infractions30Days.count} count", true);
            embed.AddField("Warning".Pluralize(data.totalInfractions.count) + " (total " + data.totalInfractions.sum + " sum of size & " + infractions.Count + " individual)",
                data.infractionStrings[0]);
            data.infractionStrings.RemoveAt(0);
            foreach (string s in data.infractionStrings)
            {
                embed.AddField("------------------------------------------------------------", s);
            }
            embed.WithAuthor(userRef);
            embed.WithFooter("ID: " + userRef.ID)
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();

            return embed.Build();
        }

        public static async Task TempBan(this UserRef userRef, TimeSpan time, string reason, SocketCommandContext context, TempActionList actions = null)
        {
            TempAct tempBan = new TempAct(userRef, time, reason);
            if (actions == null) actions = context.Guild.LoadFromFile<TempActionList>(true);
            actions.tempBans.Add(tempBan);
            actions.SaveToFile();
            await context.Guild.AddBanAsync(userRef.ID, reason: reason);
            DiscordLogging.LogTempAct(context.Guild, context.User, userRef, "bann", reason, context.Message.GetJumpUrl(), time);
            if (userRef.user != null)
            {
                try
                {
                    await userRef.user.Notify($"tempbanned for {time.LimitedHumanize()}", reason, context.Guild, context.Message.Author);
                }
                catch (Exception e)
                {
                    if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
                }
            }
            userRef.ID.RecordAct(context.Guild, tempBan, "tempban", context.Message.GetJumpUrl());
        }

        public static async Task TempMute(this UserRef userRef, TimeSpan time, string reason, SocketCommandContext context, ModerationSettings settings, TempActionList actions = null)
        {
            TempAct tempMute = new TempAct(userRef.ID, time, reason);
            if (actions == null) actions = context.Guild.LoadFromFile<TempActionList>(true);
            actions.tempMutes.Add(tempMute);
            actions.SaveToFile();
            await userRef.gUser?.AddRoleAsync(context.Guild.GetRole(settings.mutedRole));
            DiscordLogging.LogTempAct(context.Guild, context.User, userRef, "mut", reason, context.Message.GetJumpUrl(), time);
            if (userRef.user != null)
            {
                try
                {
                    await userRef.user?.Notify($"tempmuted for {time.LimitedHumanize()}", reason, context.Guild, context.Message.Author);
                }
                catch (Exception e)
                {
                    if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
                }
            }
            userRef.ID.RecordAct(context.Guild, tempMute, "tempmute", context.Message.GetJumpUrl());
        }

        public static async Task Notify(this IUser user, string action, string reason, IGuild guild, SocketUser author = null, string article = "from")
        {
            var embed = new EmbedBuilder();
            embed.WithTitle($"You have been {action} {article} a discord guild");
            embed.AddField("Reason", reason, true);
            embed.AddField("Guild name", guild.Name, true);
            embed.WithCurrentTimestamp();
            if (author != null) embed.WithAuthor(author);
            user.TryNotify(embed.Build());
        }
    }
}
