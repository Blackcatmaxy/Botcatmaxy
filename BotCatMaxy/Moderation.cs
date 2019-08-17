using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCatMaxy.Settings;
using Discord.WebSocket;
using Discord.Commands;
using BotCatMaxy.Data;
using System.Linq;
using BotCatMaxy;
using Humanizer;
using System.IO;
using Discord;
using System;

namespace BotCatMaxy {
    [Serializable]
    public struct Infraction {
        public DateTime time;
        public string logLink;
        public string reason;
        public float size;
    }

    public static class ModerationFunctions {
        public static void CheckDirectories(this string path) {
            if (!Directory.Exists(path + "/Infractions/")) {
                Directory.CreateDirectory(path + "/Infractions/");
            }
            if (!Directory.Exists(path + "/Infractions/Discord/")) {
                Directory.CreateDirectory(path + "/Infractions/Discord/");
            }
        }

        public static async Task Warn(this SocketGuildUser user, float size, string reason, SocketCommandContext context, string dir = "Discord", string logLink = null) {
            try {
                if (user.CantBeWarned()) {
                    await context.Channel.SendMessageAsync("This person can't be warned");
                    return;
                }

                if (size > 999 || size < 0.01) {
                    await context.Channel.SendMessageAsync("Why would you need to warn someone with that size?");
                    return;
                }

                List<Infraction> infractions = user.LoadInfractions(dir, true);
                Infraction newInfraction = new Infraction {
                    reason = reason,
                    time = DateTime.Now,
                    size = size
                };
                if (!logLink.IsNullOrEmpty()) newInfraction.logLink = logLink;
                infractions.Add(newInfraction);
                user.SaveInfractions(infractions, dir);

                IUser[] users = await context.Channel.GetUsersAsync().Flatten().ToArray();
                if (!users.Contains(user)) {
                    IDMChannel DM = await user.GetOrCreateDMChannelAsync();
                    if (DM != null)
                        await DM.SendMessageAsync("You have been warned in " + context.Guild.Name + " discord for \"" + reason + "\" in a channel you can't view");
                }
            } catch (Exception e) {
                await new LogMessage(LogSeverity.Error, "Warn", "An exception has happened while warning", e).Log();
            }
        }
        struct InfractionsInDays {
            public float sum;
            public int count;

            public InfractionsInDays(int x, float y) {
                sum = y;
                count = x;
            }
        }

        public static Embed CheckInfractions(this SocketGuildUser user, string dir = "Discord", int amount = 5, bool showLinks = false) {
            List<Infraction> infractions = user.LoadInfractions(dir, false);
            List<string> infractionStrings = new List<string>();
            infractionStrings.Add("");

            InfractionsInDays infractionsToday = new InfractionsInDays(0, 0);
            InfractionsInDays infractions30Days = new InfractionsInDays(0, 0);
            InfractionsInDays totalInfractions = new InfractionsInDays(0, 0);
            InfractionsInDays infractions7Days = new InfractionsInDays(0, 0);
            string plural = "";
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
                string timeAgo = dateAgo.Humanize(2);

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
                    if (showLinks && !infraction.logLink.IsNullOrEmpty()) jumpLink = $" [[Logged Here]({infraction.logLink})]";
                    string s = "[" + MathF.Abs(i - infractions.Count) + "] " + size + infraction.reason + jumpLink + " - " + timeAgo;
                    n++;

                    if ((infractionStrings.LastOrDefault() + s).Length < 1024) {
                        if (infractionStrings.LastOrDefault() != "") infractionStrings[infractionStrings.Count - 1] += "\n";
                        infractionStrings[infractionStrings.Count - 1] += s;
                    } else {
                        infractionStrings.Add(s);
                    }
                }
            }

            if (infractions.Count > 1) {
                plural = "s";
            } else {
                plural = "";
            }

            //Builds infraction embed
            var embed = new EmbedBuilder();
            embed.AddField("Today",
                $"{infractionsToday.sum} sum**|**{infractionsToday.count} count", true);
            embed.AddField("Last 7 days",
                $"{infractions7Days.sum} sum**|**{infractions7Days.count} count", true);
            embed.AddField("Last 30 days",
                $"{infractions30Days.sum} sum**|**{infractions30Days.count} count", true);
            embed.AddField("Warning" + plural + " (total " + totalInfractions.sum + " sum of size & " + infractions.Count + " individual)",
                infractionStrings[0]);
            infractionStrings.RemoveAt(0);
            foreach (string s in infractionStrings) {
                embed.AddField("------------------------------------------------------------", s);
            }
            embed.WithAuthor(user)
                .WithFooter("ID: " + user.Id)
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();
            return embed.Build();
        }

        public static async Task TempBan(this SocketGuildUser user, TimeSpan time, string reason, SocketCommandContext context, List<TempAct> tempBans = null) {
            TempAct tempBan = new TempAct(user.Id, time, reason);
            if (tempBans == null) tempBans = context.Guild.LoadFromFile<List<TempAct>>("tempBans.json", true);
            tempBans.Add(tempBan);
            tempBans.SaveToFile("tempBans.json", context.Guild);
            try {
                await user.Notify($"tempbanned for {time.Humanize()}", reason, context.Guild, context.Message.Author);
            } catch  (Exception e) {
                if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
            }
            await context.Guild.AddBanAsync(user, reason: reason);
            Logging.LogTempAct(context.Guild, context.User, user, "bann", reason, context.Message.GetJumpUrl(), time);
        }

        public static async Task TempMute(this SocketGuildUser user, TimeSpan time, string reason, SocketCommandContext context, ModerationSettings settings, List<TempAct> tempMutes = null) {
            TempAct tempMute = new TempAct(user.Id, time, reason);
            if (tempMutes == null) tempMutes = context.Guild.LoadFromFile<List<TempAct>>("tempMutes.json", true);
            tempMutes.Add(tempMute);
            tempMutes.SaveToFile("tempMutes.json", context.Guild);
            try {
                await user.Notify($"tempmuted for {time.Humanize()}", reason, context.Guild, context.Message.Author);
            } catch (Exception e) {
                if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
            }
            await user.AddRoleAsync(context.Guild.GetRole(settings.mutedRole));
            Logging.LogTempAct(context.Guild, context.User, user, "mut", reason, context.Message.GetJumpUrl(), time);
        }

        public static async Task Notify(this SocketGuildUser user, string action, string reason, SocketGuild guild, SocketUser author = null) {
            var embed = new EmbedBuilder();
            embed.WithTitle($"You have been {action} from a discord guild");
            embed.AddField("Reason", reason, true);
            embed.AddField("Guild name", guild.Name, true);
            embed.WithCurrentTimestamp();
            if (author != null) embed.WithAuthor(author);

            IDMChannel DMChannel = await user.GetOrCreateDMChannelAsync();
            if (DMChannel != null) {
                _ = DMChannel.SendMessageAsync(embed: embed.Build());
            }
        }
    }

    [Group("games")]
    [Alias("game")]
    [RequireContext(ContextType.Guild)]
    public class GameWarnModule : ModuleBase<SocketCommandContext> {
        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserAsync(SocketGuildUser user, float size, [Remainder] string reason) {
            await user.Warn(size, reason, Context, "Games");

            await ReplyAsync(user.Mention + " has gotten their " + user.LoadInfractions("Games").Count.Suffix() + " infraction for " + reason);
        }

        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserSmallSizeAsync(SocketGuildUser user, [Remainder] string reason) {
            await user.Warn(1, reason, Context, "Games");

            await ReplyAsync(user.Mention + " has gotten their " + user.LoadInfractions("Games").Count.Suffix() + " infraction for " + reason);
        }

        [Command("warns")]
        [RequireContext(ContextType.Guild)]
        [Alias("infractions", "warnings")]
        public async Task CheckUserWarnsAsync(SocketGuildUser user = null, int amount = 5) {
            if (user == null) {
                user = Context.Message.Author as SocketGuildUser;
            }

            string guildDir = Context.Guild.GetPath(false);

            if (Directory.Exists(guildDir) && File.Exists(guildDir + "/Infractions/Games/" + user.Id)) {
                await ReplyAsync(embed: user.CheckInfractions("Games", amount));
            } else {
                await ReplyAsync(user.Username + " has no warns");
            }
        }

        [Command("removewarn")]
        [Alias("warnremove", "removewarning")]
        [HasAdmin()]
        public async Task RemooveWarnAsync(SocketGuildUser user, int index) {
            string guildDir = user.Guild.GetPath(true);
            if (guildDir != null && File.Exists(guildDir + "/Infractions/Games/" + user.Id)) {
                List<Infraction> infractions = user.LoadInfractions();

                if (infractions.Count < index || index <= 0) {
                    await ReplyAsync("invalid infraction number");
                } else if (infractions.Count == 1) {
                    await ReplyAsync("removed " + user.Username + "'s warning for " + infractions[0]);
                    File.Delete(guildDir + "/Infractions/Games/" + user.Id);
                } else {
                    string reason = infractions[index - 1].reason;
                    infractions.RemoveAt(index - 1);

                    user.SaveInfractions(infractions, "Games");

                    await ReplyAsync("Removed " + user.Mention + "'s warning for " + reason);
                }
            } else {
                await ReplyAsync(user.Username + " has no warns");
            }
        }
    }

    [RequireContext(ContextType.Guild)]
    public class ModerationCommands : ModuleBase<SocketCommandContext> {
        [Command("moderationInfo")]
        public async Task ModerationInfo() {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>("moderationSettings.txt");
            if (settings == null) {
                _ = ReplyAsync("Moderation settings are null");
                return;
            }

            var embed = new EmbedBuilder();
            string rolesAbleToWarn = "";
            foreach (SocketRole role in Context.Guild.Roles) {
                if (role.Permissions.KickMembers && !role.IsManaged) {
                    if (rolesAbleToWarn != "") {
                        rolesAbleToWarn += "\n";
                    }
                    if (role.IsMentionable) {
                        rolesAbleToWarn += role.Mention;
                    } else {
                        rolesAbleToWarn += role.Name;
                    }
                }
            }

            if (settings.allowedLinks == null || settings.allowedLinks.Count == 0) {
                embed.AddField("Are links allowed?", "Links are not auto-moderated  ", true);
            } else {
                embed.AddField("Are links allowed?", "Links are auto-moderated  ", true);
            }

            if (settings.ableToWarn != null && settings.ableToWarn.Count > 0) {
                foreach (ulong roleID in settings.ableToWarn) {
                    SocketRole role = Context.Guild.GetRole(roleID);
                    if (role != null) {
                        if (rolesAbleToWarn != "") {
                            rolesAbleToWarn += "\n";
                        }
                        if (role.IsMentionable) {
                            rolesAbleToWarn += role.Mention;
                        } else {
                            rolesAbleToWarn += role.Name;
                        }
                    } else {
                        settings.ableToWarn.Remove(roleID);
                    }
                }
            }
            embed.AddField("Roles that can warn", rolesAbleToWarn, true);
            embed.AddField("Will invites lead to warn", !settings.invitesAllowed, true);
            await ReplyAsync(embed: embed.Build());
        }
    }

    [Group("discord")]
    [Alias("general", "chat", "")]
    [RequireContext(ContextType.Guild)]
    public class DiscordWarnModule : ModuleBase<SocketCommandContext> {
        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserAsync(SocketGuildUser user, [Remainder] string reason = "Unspecified") {
            string jumpLink = Logging.LogWarn(Context.Guild, Context.Message.Author, user, reason, Context.Message.GetJumpUrl());
            await user.Warn(1, reason, Context, logLink: jumpLink);

            await ReplyAsync(user.Mention + " has gotten their " + user.LoadInfractions().Count.Suffix() + " infraction for " + reason);
        }

        [Command("warn")]
        [CanWarn()]
        public async Task WarnWithSizeUserAsync(SocketGuildUser user, float size, [Remainder] string reason = "Unspecified") {
            string jumpLink = Logging.LogWarn(Context.Guild, Context.Message.Author, user, reason, Context.Message.GetJumpUrl());
            await user.Warn(size, reason, Context, logLink: jumpLink);

            await ReplyAsync(user.Mention + " has gotten their " + user.LoadInfractions().Count.Suffix() + " infraction for " + reason);
        }

        [Command("dmwarns")]
        [RequireContext(ContextType.Guild)]
        [Alias("dminfractions", "dmwarnings")]
        public async Task DMUserWarnsAsync(SocketGuildUser user = null, int amount = 10) {
            if (amount < 1) {
                await ReplyAsync("Why would you want to see that many infractions?");
                return;
            }

            if (user == null) {
                user = Context.Message.Author as SocketGuildUser;
            }
            string username;
            if (!user.Nickname.IsNullOrEmpty()) username = user.Nickname.StrippedOfPing();
            else username = user.Username.StrippedOfPing();
            if (Directory.Exists(Context.Guild.GetPath(false)) && File.Exists(Context.Guild.GetPath(false) + "/Infractions/Discord/" + user.Id)) {
                try {
                    await Context.Message.Author.GetOrCreateDMChannelAsync().Result.SendMessageAsync(embed: user.CheckInfractions(amount: amount));
                } catch {
                    await ReplyAsync("Something went wrong DMing you their infractions. Check your privacy settings and make sure the amount isn't too high");
                    return;
                }
            } else {
                await ReplyAsync(username + " has no warns");
                return;
            }

            List<Infraction> infractions = user.LoadInfractions();
            string quantity = "infraction".ToQuantity(infractions.Count);
            if (amount >= infractions.Count)
                await ReplyAsync($"DMed you {username}'s {quantity}");
            else await ReplyAsync($"DMed you {username}'s last {amount} out of {quantity}");
        }

        [Command("warns")]
        [RequireContext(ContextType.Guild)]
        [Alias("infractions", "warnings")]
        public async Task CheckUserWarnsAsync(SocketGuildUser user = null, int amount = 5) {
            if (user == null) {
                user = Context.Message.Author as SocketGuildUser;
            }
            if (!(Context.Message.Author as SocketGuildUser).CanWarn()) {
                await ReplyAsync("To avoid flood only people who can warn can use this command. Please use !dmwarns instead");
                return;
            }
            if (Directory.Exists(Context.Guild.GetPath(false)) && File.Exists(Context.Guild.GetPath(false) + "/Infractions/Discord/" + user.Id)) {
                await ReplyAsync(embed: user.CheckInfractions(amount: amount, showLinks: true));
            } else {
                await ReplyAsync(user.Username + " has no warns");
            }
        }

        [Command("removewarn")]
        [Alias("warnremove", "removewarning")]
        [HasAdmin()]
        public async Task RemoveWarnAsync(SocketGuildUser user, int index) {
            string guildDir = user.Guild.GetPath(false);
            if (guildDir != null && File.Exists(guildDir + "/Infractions/Discord/" + user.Id)) {
                List<Infraction> infractions = user.LoadInfractions();

                if (infractions.Count < index || index <= 0) {
                    await ReplyAsync("invalid infraction number");
                } else if (infractions.Count == 1) {
                    await ReplyAsync("removed " + user.Username + "'s warning for " + infractions[index - 1].reason);
                    File.Delete(guildDir + "/Infractions/Discord/" + user.Id);
                } else {
                    string reason = infractions[index - 1].reason;
                    infractions.RemoveAt(index - 1);

                    user.SaveInfractions(infractions, "Discord");

                    await ReplyAsync("Removed " + user.Mention + "'s warning for " + reason);
                }
            } else {
                await ReplyAsync(user.Username + " has no warns");
            }
        }

        [Command("kickwarn")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn(SocketGuildUser user, [Remainder] string reason = "Unspecified") {
            await user.Warn(1, reason, Context, "Discord");

            _ = user.Notify("kicked", reason, Context.Guild, Context.Message.Author);
            await ReplyAsync(user.Mention + " has been kicked for " + reason);
            await user.KickAsync(reason);
        }

        [Command("kickwarn")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn(SocketGuildUser user, float size, [Remainder] string reason = "Unspecified") {
            await user.Warn(size, reason, Context, "Discord");

            _ = user.Notify("kicked", reason, Context.Guild, Context.Message.Author);
            await ReplyAsync(user.Mention + " has been kicked for " + reason);
            await user.KickAsync(reason);
        }

        [Command("tempban")]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task TempBanUser(SocketGuildUser user, string time, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-ban for less than a minute");
                return;
            }
            List<TempAct> tempBans = Context.Guild.LoadFromFile<List<TempAct>>("tempBans.json", true);
            if (!tempBans.IsNullOrEmpty() && tempBans.Any(tempBan => tempBan.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-banned");
                return;
            }
            IUserMessage message = await ReplyAsync($"Temporarily banning {user.Mention} for {amount.Value.Humanize()} because of {reason}");
            await user.TempBan(amount.Value, reason, Context, tempBans);
            _ = message.ModifyAsync(msg => msg.Content = $"Temporarily banned {user.Mention} for {amount.Value.Humanize()} because of {reason}");
        }

        [Command("tempmute")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempMuteUser(SocketGuildUser user, string time, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-mute for less than a minute");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>("moderationSettings.txt");
            if (settings == null || settings.mutedRole == 0 || Context.Guild.GetRole(settings.mutedRole) == null) {
                await ReplyAsync("Muted role is null or invalid");
                return;
            }
            List<TempAct> tempMutes = Context.Guild.LoadFromFile<List<TempAct>>("tempMutes.json", true);
            if (!tempMutes.IsNullOrEmpty() && tempMutes.Any(tempMute => tempMute.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-muted");
                return;
            }

            IUserMessage message = await ReplyAsync($"Temporarily muting {user.Mention} for {amount.Value.Humanize()} because of {reason}");
            await user.TempMute(amount.Value, reason, Context, settings);
            _ = message.ModifyAsync(msg => msg.Content = $"Temporarily muted {user.Mention} for {amount.Value.Humanize()} because of {reason}");
        }
    }
}