using MongoDB.Bson.Serialization.Attributes;
using System.Text.RegularExpressions;
using Discord.Addons.Preconditions;
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
    public class Infraction {
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
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

        public static async Task Warn(this SocketGuildUser user, float size, string reason, SocketCommandContext context, string logLink = null) {
            try {
                if (user.CantBeWarned()) {
                    await context.Channel.SendMessageAsync("This person can't be warned");
                    return;
                }

                if (size > 999 || size < 0.01) {
                    await context.Channel.SendMessageAsync("Why would you need to warn someone with that size?");
                    return;
                }

                List<Infraction> infractions = user.LoadInfractions(true);
                Infraction newInfraction = new Infraction {
                    reason = reason,
                    time = DateTime.Now,
                    size = size
                };
                if (!logLink.IsNullOrEmpty()) newInfraction.logLink = logLink;
                infractions.Add(newInfraction);
                user.SaveInfractions(infractions);

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
            public InfractionInfo(List<Infraction> infractions,int amount = 5, bool showLinks = false) {
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
                        string timeAgo = dateAgo.Humanize(2);
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

        public static async Task TempBan(this SocketGuildUser user, TimeSpan time, string reason, SocketCommandContext context, TempActionList actions = null) {
            TempAct tempBan = new TempAct(user.Id, time, reason);
            if (actions == null) actions = context.Guild.LoadFromFile<TempActionList>(true);
            actions.tempBans.Add(tempBan);
            actions.SaveToFile(context.Guild);
            try {
                await user.Notify($"tempbanned for {time.Humanize()}", reason, context.Guild, context.Message.Author);
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
                await user.Notify($"tempmuted for {time.Humanize()}", reason, context.Guild, context.Message.Author);
            } catch (Exception e) {
                if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
            }
            await user.AddRoleAsync(context.Guild.GetRole(settings.mutedRole));
            Logging.LogTempAct(context.Guild, context.User, user, "mut", reason, context.Message.GetJumpUrl(), time);
        }

        public static async Task Notify(this IUser user, string action, string reason, SocketGuild guild, SocketUser author = null) {
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

    [RequireContext(ContextType.Guild)]
    public class DiscordModModule : ModuleBase<SocketCommandContext> {
        [Ratelimit(2, 1, Measure.Minutes)]
        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserAsync(SocketGuildUser user, [Remainder] string reason = "Unspecified") {
            string jumpLink = Logging.LogWarn(Context.Guild, Context.Message.Author, user, reason, Context.Message.GetJumpUrl());
            await user.Warn(1, reason, Context, logLink: jumpLink);

            await ReplyAsync(user.Mention + " has gotten their " + user.LoadInfractions().Count.Suffix() + " infraction for " + reason);
        }

        [Ratelimit(2, 1, Measure.Minutes)]
        [Command("warn")]
        [CanWarn()]
        public async Task WarnWithSizeUserAsync(SocketGuildUser user, float size, [Remainder] string reason = "Unspecified") {
            string jumpLink = Logging.LogWarn(Context.Guild, Context.Message.Author, user, reason, Context.Message.GetJumpUrl());
            await user.Warn(size, reason, Context, logLink: jumpLink);

            await ReplyAsync(user.Mention + " has gotten their " + user.LoadInfractions().Count.Suffix() + " infraction for " + reason);
        }

        [Ratelimit(3, 5, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        [Command("dmwarns")]
        [RequireContext(ContextType.Guild)]
        [Alias("dminfractions", "dmwarnings")]
        public async Task DMUserWarnsAsync(SocketGuildUser user = null, int amount = 99) {
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

            List<Infraction> infractions = user.LoadInfractions(false);
            if (!infractions.IsNullOrEmpty()) {
                try {
                    await Context.Message.Author.GetOrCreateDMChannelAsync().Result.SendMessageAsync(embed: infractions.GetEmbed(user, amount: amount));
                } catch {
                    await ReplyAsync("Something went wrong DMing you their infractions. Check your privacy settings and make sure the amount isn't too high");
                    return;
                }
            } else {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} has no infractions");
                return;
            }
            string quantity = "infraction".ToQuantity(infractions.Count);
            if (amount >= infractions.Count) await ReplyAsync($"DMed you {username}'s {quantity}");
            else await ReplyAsync($"DMed you {username}'s last {amount} out of {quantity}");
        }

        [Command("warns")]
        [RequireContext(ContextType.Guild)]
        [Alias("infractions", "warnings")]
        [Ratelimit(3, 2, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        public async Task CheckUserWarnsAsync(SocketGuildUser user = null, int amount = 5) {
            if (user == null) {
                user = Context.Message.Author as SocketGuildUser;
            }
            if (!(Context.Message.Author as SocketGuildUser).CanWarn()) {
                await ReplyAsync("To avoid flood only people who can warn can use this command. Please use !dmwarns instead");
                return;
            }

            List<Infraction> infractions = user.LoadInfractions(false);
            if (infractions.IsNullOrEmpty()) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} has no infractions");
                return;
            }
            await ReplyAsync(embed: infractions.GetEmbed(user, amount: amount, showLinks: true));
        }

        [Command("removewarn")]
        [Alias("warnremove", "removewarning")]
        [HasAdmin()]
        [Ratelimit(3, 1, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        public async Task RemoveWarnAsync(SocketGuildUser user, int index) {
            List<Infraction> infractions = user.LoadInfractions();
            if (infractions.IsNullOrEmpty()) {
                await ReplyAsync("Infractions are null");
                return;
            }
            if (infractions.Count < index || index <= 0) {
                await ReplyAsync("Invalid infraction number");
                return;
            }
            string reason = infractions[index - 1].reason;
            infractions.RemoveAt(index - 1);

            user.SaveInfractions(infractions);
            await ReplyAsync("Removed " + user.Mention + "'s warning for " + reason);
        }

        [Ratelimit(3, 1, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        [Command("kickwarn")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn(SocketGuildUser user, [Remainder] string reason = "Unspecified") {
            await user.Warn(1, reason, Context, "Discord");

            _ = user.Notify("kicked", reason, Context.Guild, Context.Message.Author);
            await ReplyAsync(user.Mention + " has been kicked for " + reason);
            await user.KickAsync(reason);
        }

        [Ratelimit(3, 1, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        [Command("kickwarn")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn(SocketGuildUser user, float size, [Remainder] string reason = "Unspecified") {
            await user.Warn(size, reason, Context, "Discord");

            _ = user.Notify("kicked", reason, Context.Guild, Context.Message.Author);
            await ReplyAsync(user.Mention + " has been kicked for " + reason);
            await user.KickAsync(reason);
        }

        [Ratelimit(3, 1, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        [Command("tempban")]
        [Alias("tban", "temp-ban")]
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
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempBans.Any(tempBan => tempBan.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-banned");
                return;
            }
            IUserMessage message = await ReplyAsync($"Temporarily banning {user.Mention} for {amount.Value.Humanize()} because of {reason}");
            await user.TempBan(amount.Value, reason, Context, actions);
            _ = message.ModifyAsync(msg => msg.Content = $"Temporarily banned {user.Mention} for {amount.Value.Humanize()} because of {reason}");
        }

        [Ratelimit(3, 1, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        [Command("tempbanwarn")]
        [Alias("tbanwarn", "temp-banwarn", "tempbanandwarn")]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task TempBanWarnUser(SocketGuildUser user, string time, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-ban for less than a minute");
                return;
            }

            await user.Warn(1, reason, Context, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempBans.Any(tempBan => tempBan.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-banned (the warn did go through)");
                return;
            }
            IUserMessage message = await ReplyAsync($"Temporarily banning {user.Mention} for {amount.Value.Humanize()} because of {reason}");
            await user.TempBan(amount.Value, reason, Context, actions);
            _ = message.ModifyAsync(msg => msg.Content = $"Temporarily banned {user.Mention} for {amount.Value.Humanize()} because of {reason}");
        }

        [Ratelimit(3, 1, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        [Command("tempbanwarn")]
        [Alias("tbanwarn", "temp-banwarn", "tempbanwarn", "warntempban")]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task TempBanWarnUser(SocketGuildUser user, string time, float size, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-ban for less than a minute");
                return;
            }

            await user.Warn(size, reason, Context, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempBans.Any(tempBan => tempBan.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-banned (the warn did go through)");
                return;
            }
            IUserMessage message = await ReplyAsync($"Temporarily banning {user.Mention} for {amount.Value.Humanize()} because of {reason}");
            await user.TempBan(amount.Value, reason, Context, actions);
            _ = message.ModifyAsync(msg => msg.Content = $"Temporarily banned {user.Mention} for {amount.Value.Humanize()} because of {reason}");
        }

        [Ratelimit(3, 1, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        [Command("tempmute")]
        [Alias("tmute", "temp-mute")]
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
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (settings == null || settings.mutedRole == 0 || Context.Guild.GetRole(settings.mutedRole) == null) {
                await ReplyAsync("Muted role is null or invalid");
                return;
            }
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempMutes.Any(tempMute => tempMute.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-muted");
                return;
            }

            IUserMessage message = await ReplyAsync($"Temporarily muting {user.Mention} for {amount.Value.Humanize()} because of {reason}");
            await user.TempMute(amount.Value, reason, Context, settings, actions);
            _ = message.ModifyAsync(msg => msg.Content = $"Temporarily muted {user.Mention} for {amount.Value.Humanize()} because of {reason}");
        }

        [Ratelimit(3, 1, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        [Command("tempmutewarn")]
        [Alias("tmutewarn", "temp-mutewarn", "warntmute", "tempmuteandwarn")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempMuteWarnUser(SocketGuildUser user, string time, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-mute for less than a minute");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (settings == null || settings.mutedRole == 0 || Context.Guild.GetRole(settings.mutedRole) == null) {
                await ReplyAsync("Muted role is null or invalid");
                return;
            }
            await user.Warn(1, reason, Context, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempMutes.Any(tempMute => tempMute.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-muted, (the warn did go through)");
                return;
            }

            IUserMessage message = await ReplyAsync($"Temporarily muting {user.Mention} for {amount.Value.Humanize()} because of {reason}");
            await user.TempMute(amount.Value, reason, Context, settings, actions);
            _ = message.ModifyAsync(msg => msg.Content = $"Temporarily muted {user.Mention} for {amount.Value.Humanize()} because of {reason}");
        }

        [Ratelimit(3, 1, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
        [Command("tempmutewarn")]
        [Alias("tmutewarn", "temp-mutewarn", "warntmute", "tempmuteandwarn")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempMuteWarnUser(SocketGuildUser user, string time, float size, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-mute for less than a minute");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (settings == null || settings.mutedRole == 0 || Context.Guild.GetRole(settings.mutedRole) == null) {
                await ReplyAsync("Muted role is null or invalid");
                return;
            }
            await user.Warn(size, reason, Context, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempMutes.Any(tempMute => tempMute.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-muted, (the warn did go through)");
                return;
            }

            IUserMessage message = await ReplyAsync($"Temporarily muting {user.Mention} for {amount.Value.Humanize()} because of {reason}");
            await user.TempMute(amount.Value, reason, Context, settings, actions);
            _ = message.ModifyAsync(msg => msg.Content = $"Temporarily muted {user.Mention} for {amount.Value.Humanize()} because of {reason}");
        }
    }
}