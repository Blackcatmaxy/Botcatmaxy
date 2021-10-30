using BotCatMaxy;
using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Services.TempActions;

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
        // To get a string variant of the user
        public static string GetTag(this IUser user)
            => $"{user.Username}#{user.Discriminator}";

        public static async Task<WarnResult> Warn(this UserRef userRef, float size, string reason, ITextChannel channel, string logLink = null)
        {
            Contract.Requires(userRef != null);
            if (userRef.GuildUser != null)
                return await userRef.GuildUser.Warn(size, reason, channel, logLink);
            else
                return await userRef.ID.Warn(size, reason, channel, userRef.User, logLink);
        }

        public static async Task<WarnResult> Warn(this IGuildUser user, float size, string reason, ITextChannel channel, string logLink = null)
        {

            if (user.HasAdmin())
            {
                return new WarnResult("This person can't be warned");
            }

            return await user.Id.Warn(size, reason, channel, user, logLink);

        }

        public static async Task<WarnResult> Warn(this ulong userID, float size, string reason, ITextChannel channel, IUser warnee = null, string logLink = null)
        {
            if (size > 999 || size < 0.01)
            {
                return new WarnResult("The infraction size must be between `0.01` and `999`.");
            }

            try
            {
                List<Infraction> infractions = userID.AddWarn(size, reason, channel.Guild, logLink);

                //Try to message but will fail if user has DMs blocked
                try
                {
                    if (warnee != null)
                    {
                        LogSettings logSettings = channel.Guild.LoadFromFile<LogSettings>(false);
                        IUser[] users = null;
                        if (logSettings?.pubLogChannel != null && channel.Guild.TryGetChannel(logSettings.pubLogChannel.Value, out IGuildChannel logChannel))
                            users = await (logChannel as IMessageChannel).GetUsersAsync().Flatten().ToArrayAsync();
                        else
                            users = await (channel as IMessageChannel).GetUsersAsync().Flatten().ToArrayAsync();
                        if (!users.Any(xUser => xUser.Id == userID))
                        {
                            warnee.TryNotify($"You have been warned in {channel.Guild.Name} discord for \"{reason}\" in a channel you can't view");
                        }
                    }
                }
                catch { }
                return new WarnResult(infractions.Count);
            }
            catch (Exception e)
            {
                List<Infraction> infractions = userID.LoadInfractions(channel.Guild, true);
                await new LogMessage(LogSeverity.Error, "Warn", $"An exception has happened while warning a user ({userID}) with {infractions.Count} warns in {await channel.Guild.Describe()}", e).Log();
                return new WarnResult(("Something has gone wrong with trying to warn this user. Please post a bug report at https://bot.blackcatmaxy.com/issues with the text below: ```" + e.ToString()).Truncate(1500) + "```");
            }
        }

        public static List<Infraction> AddWarn(this ulong userID, float size, string reason, IGuild guild, string logLink)
        {
            List<Infraction> infractions = userID.LoadInfractions(guild, true);
            Infraction newInfraction = new Infraction
            {
                Reason = reason,
                Time = DateTime.UtcNow,
                Size = size,
                LogLink = logLink
            };
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
                    TimeSpan dateAgo = DateTime.UtcNow.Subtract(infraction.Time);
                    totalInfractions.sum += infraction.Size;
                    totalInfractions.count++;
                    if (dateAgo.Days <= 7)
                    {
                        infractions7Days.sum += infraction.Size;
                        infractions7Days.count++;
                    }
                    if (dateAgo.Days <= 30)
                    {
                        infractions30Days.sum += infraction.Size;
                        infractions30Days.count++;
                        if (dateAgo.Days < 1)
                        {
                            infractionsToday.sum += infraction.Size;
                            infractionsToday.count++;
                        }
                    }

                    string size = "";
                    if (infraction.Size != 1)
                    {
                        size = "(" + infraction.Size + "x) ";
                    }

                    if (n < amount)
                    {
                        string jumpLink = "";
                        string timeAgo = dateAgo.LimitedHumanize(2);
                        if (showLinks && !infraction.LogLink.IsNullOrEmpty()) jumpLink = $" [[Logged Here]({infraction.LogLink})]";
                        string infracInfo = $"[{MathF.Abs(i - infractions.Count)}] {size}{infraction.Reason}{jumpLink} - {timeAgo}";
                        n++;

                        //So we don't go over embed character limit of 9000
                        if (infractionStrings.Select(str => str.Length).Sum() + infracInfo.Length >= 5800)
                            return;

                        if ((infractionStrings.LastOrDefault() + infracInfo).Length < 1024)
                        {
                            if (infractionStrings.LastOrDefault()?.Length is not null or 0) infractionStrings[infractionStrings.Count - 1] += "\n";
                            infractionStrings[^1] += infracInfo;
                        }
                        else
                        {
                            infractionStrings.Add(infracInfo);
                        }
                    }
                }
            }
        }

        public static Embed GetEmbed(this List<Infraction> infractions, UserRef userRef, IGuild guild, int amount = 5, bool showLinks = false)
        {
            InfractionInfo data = new(infractions, amount, showLinks);

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
                embed.AddField("------------------------------------------------------------", s);
            embed.WithAuthor(userRef)
                .WithGuildAsFooter(guild, "ID: " + userRef.ID)
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            return embed.Build();
        }

        public static async Task TempBan(this UserRef userRef, TimeSpan time, string reason, ICommandContext context, TempActionList actions = null)
        {
            var tempBan = new TempBan {Length = time, Reason = reason, UserId = userRef.ID};
            actions ??= context.Guild.LoadFromFile<TempActionList>(true);
            actions.tempBans.Add(tempBan);
            actions.SaveToFile();
            await context.Guild.AddBanAsync(userRef.ID, reason: reason);
            DiscordLogging.LogTempAct(context.Guild, context.User, userRef, "bann", reason, context.Message.GetJumpUrl(), time);
            if (userRef.User != null)
            {
                try
                {
                    await userRef.User.Notify($"tempbanned for {time.LimitedHumanize()}", reason, context.Guild, context.Message.Author);
                }
                catch (Exception e)
                {
                    if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
                }
            }
            userRef.ID.RecordAct(context.Guild, tempBan, "tempban", context.Message.GetJumpUrl());
        }

        public static async Task<RuntimeResult> TempMute(this UserRef userRef, TimeSpan time, string reason, ICommandContext context)
        {
            if (time.TotalMinutes < 1)
                return CommandResult.FromError("Can't temp-mute for less than a minute");
            var settings = context.Guild.LoadFromFile<ModerationSettings>(false);
            if (!(context.Message.Author as IGuildUser).HasAdmin())
            {
                if (settings?.maxTempAction != null && time > settings.maxTempAction)
                    return CommandResult.FromError("You are not allowed to punish for that long");
            }
            var role = context.Guild.GetRole(settings?.mutedRole ?? 0);
            if (role == null)
                return CommandResult.FromError("Muted role is null or invalid");
            var tempMute = new TempMute
                {
                    Length = time,
                    RoleId = role.Id,
                    Reason = reason,
                    UserId = userRef.ID
                };
            var actions = context.Guild.LoadFromFile<TempActionList>(true);
            actions.tempMutes.Add(tempMute);
            actions.SaveToFile();
            if (userRef.GuildUser != null) 
                await userRef.GuildUser.AddRoleAsync(role);
            DiscordLogging.LogTempAct(context.Guild, context.User, userRef, "mut", reason, context.Message.GetJumpUrl(), time);
            if (userRef.User != null)
            {
                try
                {
                    await userRef.User?.Notify($"tempmuted for {time.LimitedHumanize()}", reason, context.Guild, context.Message.Author);
                }
                catch (Exception e)
                {
                    if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
                }
            }
            userRef.ID.RecordAct(context.Guild, tempMute, "tempmute", context.Message.GetJumpUrl());
            return null;
        }
    }
}
