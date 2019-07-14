﻿using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCatMaxy.Settings;
using Discord.WebSocket;
using Discord.Commands;
using BotCatMaxy.Data;
using System.Linq;
using BotCatMaxy;
using System.IO;
using Discord;
using System;

namespace BotCatMaxy {
    [Serializable]
    public struct Infraction {
        public DateTime time;
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

        public static async Task Warn(this SocketGuildUser user, float size, string reason, SocketCommandContext context, string dir = "Discord") {
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
            infractions.Add(newInfraction);
            user.SaveInfractions(infractions, dir);

            IUser[] users = await context.Channel.GetUsersAsync().Flatten().ToArray();
            if (!users.Contains(user)) {
                IDMChannel DM = await user.GetOrCreateDMChannelAsync();
                if (DM != null)
                _ = DM.SendMessageAsync("You have been warned in " + context.Guild.Name + " discord for \"" + reason + "\" in a channel you can't view");
            }
        }

        public static Embed CheckInfractions(this SocketGuildUser user, string dir = "Discord", int amount = 5) {
            List<Infraction> infractions = user.LoadInfractions(dir, false);

            string infractionList = "";
            float infractionsToday = 0;
            float infractions30Days = 0;
            float totalInfractions = 0;
            float last7Days = 0;
            string plural = "";
            infractions.Reverse();
            if (infractions.Count < amount) {
                amount = infractions.Count;
            }
            int n = 0;
            for (int i = 0; i < infractions.Count; i++) {
                if (i != 0) { //Creates new line if it's not the first infraction
                    infractionList += "\n";
                }
                Infraction infraction = infractions[i];

                //Gets how long ago all the infractions were
                TimeSpan dateAgo = DateTime.Now.Subtract(infraction.time);
                totalInfractions += infraction.size;
                string timeAgo = MathF.Round(dateAgo.Days / 30) + " months ago";

                if (dateAgo.Days <= 7) {
                    last7Days += infraction.size;
                }
                if (dateAgo.Days <= 30) {
                    if (dateAgo.Days == 1) {
                        plural = "";
                    } else {
                        plural = "s";
                    }
                    infractions30Days += infraction.size;
                    timeAgo = dateAgo.Days + " day" + plural + " ago";
                    if (dateAgo.Days < 1) {
                        infractionsToday += infraction.size;
                        if (dateAgo.Hours == 1) {
                            plural = "";
                        } else {
                            plural = "s";
                        }
                        timeAgo = dateAgo.Hours + " hour" + plural + " ago";
                        if (dateAgo.Hours < 1) {
                            if (dateAgo.Minutes == 1) {
                                plural = "";
                            } else {
                                plural = "s";
                            }
                            timeAgo = dateAgo.Minutes + " minute" + plural + " ago";
                            if (dateAgo.Minutes < 1) {
                                if (dateAgo.Seconds == 1) {
                                    plural = "";
                                } else {
                                    plural = "s";
                                }
                                if (dateAgo.Seconds == 0) {
                                    timeAgo = dateAgo.TotalSeconds + " second" + plural + " ago";
                                } else {
                                    timeAgo = dateAgo.Seconds + " second" + plural + " ago";
                                }
                            }
                        }
                    }
                }

                string size = "";
                if (infraction.size != 1) {
                    size = "(" + infraction.size + "x) ";
                }

                if (n < amount) {
                    infractionList += "[" + MathF.Abs(i - infractions.Count) + "] " + size + infraction.reason + " - " + timeAgo;
                    n++;
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
                infractionsToday, true);
            embed.AddField("Last 7 days",
                last7Days, true);
            embed.AddField("Last 30 days",
                infractions30Days, true);
            embed.AddField("Warning" + plural + " (total " + totalInfractions + " sum of size & " + infractions.Count + " individual)",
                infractionList)
                .WithAuthor(user)
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();
            return embed.Build();
        }

        public static async Task NotifyOfKick(this SocketGuildUser user, string reason, SocketCommandContext context) {
            var embed = new EmbedBuilder();
            embed.WithTitle("You have been kicked from a discord guild");
            embed.AddField("Reason", reason, true);
            embed.AddField("Guild name", context.Guild.Name, true);
            embed.WithCurrentTimestamp();
            embed.WithAuthor(context.Message.Author);

            IDMChannel DMChannel = await user.GetOrCreateDMChannelAsync();
            if (DMChannel != null) {
                await DMChannel.SendMessageAsync(embed: embed.Build());
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
            ModerationSettings settings = Context.Guild.LoadModSettings(false);
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
            _ = user.Warn(1, reason, Context);
            _ = Logging.LogWarn(Context.Guild, Context.Message.Author, user, reason);

            await ReplyAsync(user.Mention + " has gotten their " + user.LoadInfractions().Count.Suffix() + " infraction for " + reason);
        }

        [Command("warn")]
        [CanWarn()]
        public async Task WarnWithSizeUserAsync(SocketGuildUser user, float size, [Remainder] string reason = "Unspecified") {
            _ = user.Warn(size, reason, Context);
            _ = Logging.LogWarn(Context.Guild, Context.Message.Author, user, reason);

            await ReplyAsync(user.Mention + " has gotten their " + user.LoadInfractions().Count.Suffix() + " infraction for " + reason);
        }

        [Command("dmwarns")]
        [RequireContext(ContextType.Guild)]
        [Alias("dminfractions", "dmwarnings")]
        public async Task DMUserWarnsAsync(SocketGuildUser user = null, int amount = 10) {
            if (user == null) {
                user = Context.Message.Author as SocketGuildUser;
            }
            if (Directory.Exists(Context.Guild.GetPath(false)) && File.Exists(Context.Guild.GetPath(false) + "/Infractions/Discord/" + user.Id)) {
                await Context.Message.Author.GetOrCreateDMChannelAsync().Result.SendMessageAsync(embed: user.CheckInfractions(amount: amount));
            } else {
                await Context.Message.Author.GetOrCreateDMChannelAsync().Result.SendMessageAsync(user.Mention + " has no warns");
            }
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
                await ReplyAsync(embed: user.CheckInfractions(amount: amount));
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

            _ = user.NotifyOfKick(reason, Context);
            await ReplyAsync(user.Mention + " has been kicked for " + reason);
            await user.KickAsync(reason);
        }

        [Command("kickwarn")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn(SocketGuildUser user, float size, [Remainder] string reason = "Unspecified") {
            await user.Warn(size, reason, Context, "Discord");

            _ = user.NotifyOfKick(reason, Context);
            await ReplyAsync(user.Mention + " has been kicked for " + reason);
            await user.KickAsync(reason);
        }

        [Command("testtempban")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task TempBan(SocketUser user, string time, [Remainder] string reason) {
            string timeUnit = "";
            string plural = "";
            int hours = 0;
            try {
                string intString = time.Remove(time.Length - 1);
                if (time.ToLower().EndsWith('d')) {
                    timeUnit = "day";
                    hours = int.Parse(intString) * 24;
                    if (hours / 24 > 1) {
                        plural = "s";
                    }
                } else if (time.ToLower().EndsWith('h')) {
                    timeUnit = "hour";
                    hours = int.Parse(intString);
                    if (hours > 1) {
                        plural = "s";
                    }
                } else {
                    await ReplyAsync("Time unit not recognized, please use \'d\' or \'h\'");
                    return;
                }
            } catch (FormatException) {
                await ReplyAsync($"Unable to parse '{time}' don't use decimals");
            }

            if (hours < 1) {
                await ReplyAsync("Can't warn for less than an hour");
                return;
            }

            IUserMessage message = await ReplyAsync("Banning " + user.Mention + " for " + hours + " " + timeUnit + plural + " because of " + reason);
            TempBan tempBan = new TempBan(user.Id, hours);
            List<TempBan> tempBans = Context.Guild.LoadTempBans(true);
            tempBans.Add(tempBan);
            tempBans.SaveTempBans(Context.Guild);
            await Context.Guild.AddBanAsync(user, reason: reason);
            _ = message.ModifyAsync(msg => msg.Content = "Banned " + user.Mention + " for " + hours + " " + timeUnit + plural + " because of " + reason);
            IDMChannel DM = await user.GetOrCreateDMChannelAsync();
            _ = DM.SendMessageAsync("You have been temp banned in " + Context.Guild.Name + " discord for \"" + reason + "\" for " + hours + " " + timeUnit + plural);
        }
    }
}