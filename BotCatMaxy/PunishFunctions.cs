using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Commands;
using BotCatMaxy.Data;
using Discord.Rest;
using System.Linq;
using BotCatMaxy;
using Humanizer;
using Discord;
using System;

namespace BotCatMaxy.Moderation {
    public static class PunishFunctions {
        public static async Task Warn(this SocketGuildUser user, float size, string reason, SocketTextChannel channel, string logLink = null) {
            try {
                if (user.CantBeWarned()) {
                    await channel.SendMessageAsync("This person can't be warned");
                    return;
                }

                await user.Id.Warn(size, reason, channel, user, logLink);
            } catch (Exception e) {
                await new LogMessage(LogSeverity.Error, "Warn", "An exception has happened while warning", e).Log();
            }
        }

        public static async Task Warn(this ulong userID, float size, string reason, SocketTextChannel channel, IUser warnee = null, string logLink = null) {
            if (size > 999 || size < 0.01) {
                await channel.SendMessageAsync("Why would you need to warn someone with that size?");
                return;
            }

            List<Infraction> infractions = userID.LoadInfractions(channel.Guild, true);
            Infraction newInfraction = new Infraction {
                reason = reason,
                time = DateTime.Now,
                size = size
            };
            if (!logLink.IsNullOrEmpty()) newInfraction.logLink = logLink;
            infractions.Add(newInfraction);
            userID.SaveInfractions(channel.Guild, infractions);

            try {
                if (warnee != null) {
                    IUser[] users = await (channel as ISocketMessageChannel).GetUsersAsync().Flatten().ToArray();
                    if (!users.Any(xUser => xUser.Id == userID)) {
                        warnee.TryNotify($"You have been warned in {channel.Guild.Name} discord for \"{reason}\" in a channel you can't view");
                    }
                }
            } catch { }
        }

        public static async Task FilterPunish(this SocketCommandContext context, string reason, ModerationSettings settings, float warnSize = 0.5f) {
            string jumpLink = Logging.LogMessage(reason, context.Message, context.Guild);
            await ((SocketGuildUser)context.User).Warn(warnSize, reason, context.Channel as SocketTextChannel, logLink: jumpLink);
            LogSettings logSettings = context.Guild.LoadFromFile<LogSettings>(false);
            Task<RestUserMessage> warnMessage = null;
            if (context.Guild.GetTextChannel(logSettings?.pubLogChannel ?? 0) != null) {
                warnMessage = context.Guild.GetTextChannel(logSettings.pubLogChannel ?? 0).SendMessageAsync($"{context.User.Mention} has been given their {(context.User as SocketGuildUser).LoadInfractions().Count.Suffix()} infraction because of {reason}");
            } else {
                if (settings?.anouncementChannels?.Contains(context.Channel.Id) ?? false) //If this channel is an anouncement channel
                    _ = context.Message.Author.Notify("warned", reason, context.Guild, article: "in");
                else
                    warnMessage = context.Channel.SendMessageAsync($"{context.User.Mention} has been given their {(context.User as SocketGuildUser).LoadInfractions().Count.Suffix()} infraction because of {reason}");
            }
            try {
                Logging.AddToDeletedCache(context.Message.Id);
                await context.Message.DeleteAsync();
            } catch {
                _ = warnMessage?.Result?.ModifyAsync(msg => msg.Content += ", something went wrong removing the message.");
            }
        }

        public struct InfractionsInDays {
            public float sum;
            public int count;
        }

        public struct InfractionInfo {
            public InfractionsInDays infractionsToday;
            public InfractionsInDays infractions30Days;
            public InfractionsInDays totalInfractions;
            public InfractionsInDays infractions7Days;
            public List<string> infractionStrings;
            public InfractionInfo(List<Infraction> infractions, int amount = 5, bool showLinks = false) {
                infractionsToday = new InfractionsInDays();
                infractions30Days = new InfractionsInDays();
                totalInfractions = new InfractionsInDays();
                infractions7Days = new InfractionsInDays();
                infractionStrings = new List<string> { "" };

                infractions.Reverse();
                if (infractions.Count < amount) {
                    amount = infractions.Count;
                }
                int n = 0;
                for (int i = 0; i < infractions.Count; i++) {
                    Infraction infraction = infractions[i];

                    //Gets how long ago all the infractions were
                    TimeSpan dateAgo = DateTime.Now.Subtract(infraction.time);
                    totalInfractions.sum += infraction.size;
                    totalInfractions.count++;
                    if (dateAgo.Days <= 7) {
                        infractions7Days.sum += infraction.size;
                        infractions7Days.count++;
                    }
                    if (dateAgo.Days <= 30) {
                        infractions30Days.sum += infraction.size;
                        infractions30Days.count++;
                        if (dateAgo.Days < 1) {
                            infractionsToday.sum += infraction.size;
                            infractionsToday.count++;
                        }
                    }

                    string size = "";
                    if (infraction.size != 1) {
                        size = "(" + infraction.size + "x) ";
                    }

                    if (n < amount) {
                        string jumpLink = "";
                        string timeAgo = dateAgo.LimitedHumanize(2);
                        if (showLinks && !infraction.logLink.IsNullOrEmpty()) jumpLink = $" [[Logged Here]({infraction.logLink})]";
                        string s = "[" + MathF.Abs(i - infractions.Count) + "] " + size + infraction.reason + jumpLink + " - " + timeAgo;
                        n++;

                        if ((infractionStrings.LastOrDefault() + s).Length < 1024) {
                            if (infractionStrings.LastOrDefault().IsNullOrEmpty()) infractionStrings[infractionStrings.Count - 1] += "\n";
                            infractionStrings[infractionStrings.Count - 1] += s;
                        } else {
                            infractionStrings.Add(s);
                        }
                    }
                }
            }
        }

        public static Embed GetEmbed(this List<Infraction> infractions, SocketGuildUser user = null, int amount = 5, bool showLinks = false) {
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
            foreach (string s in data.infractionStrings) {
                embed.AddField("------------------------------------------------------------", s);
            }
            if (user != null) {
                embed.WithAuthor(user)
                .WithFooter("ID: " + user.Id)
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();
            }

            return embed.Build();
        }

        public struct EmbedResult {
            public Embed embed;
        }

        public static async Task TempBan(this SocketGuildUser user, TimeSpan time, string reason, SocketCommandContext context, TempActionList actions = null) {
            TempAct tempBan = new TempAct(user.Id, time, reason);
            if (actions == null) actions = context.Guild.LoadFromFile<TempActionList>(true);
            actions.tempBans.Add(tempBan);
            actions.SaveToFile(context.Guild);
            try {
                await user.Notify($"tempbanned for {time.LimitedHumanize()}", reason, context.Guild, context.Message.Author);
            } catch (Exception e) {
                if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
            }
            await context.Guild.AddBanAsync(user, reason: reason);
            Logging.LogTempAct(context.Guild, context.User, user, "bann", reason, context.Message.GetJumpUrl(), time);
        }

        public static async Task TempMute(this SocketGuildUser user, TimeSpan time, string reason, SocketCommandContext context, ModerationSettings settings, TempActionList actions = null) {
            TempAct tempMute = new TempAct(user.Id, time, reason);
            if (actions == null) actions = context.Guild.LoadFromFile<TempActionList>(true);
            actions.tempMutes.Add(tempMute);
            actions.SaveToFile(context.Guild);
            try {
                await user.Notify($"tempmuted for {time.LimitedHumanize()}", reason, context.Guild, context.Message.Author);
            } catch (Exception e) {
                if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
            }
            await user.AddRoleAsync(context.Guild.GetRole(settings.mutedRole));
            Logging.LogTempAct(context.Guild, context.User, user, "mut", reason, context.Message.GetJumpUrl(), time);
        }

        public static async Task Notify(this IUser user, string action, string reason, IGuild guild, SocketUser author = null, string article = "from") {
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
