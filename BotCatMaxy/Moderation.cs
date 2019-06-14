using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using BotCatMaxy.Settings;
using BotCatMaxy;
using System.Linq;
using Discord.Rest;
using BotCatMaxy.Data;
using System.Text.RegularExpressions;
//using Discord.Addons.Preconditions;

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

                    await ReplyAsync("removed " + user.Mention + "'s warning for " + reason);
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
                embed.AddField("Are links allowed?", "Links are not auto-moderated", true);
            } else {
                embed.AddField("Are links allowed?", "Links are auto-moderated", true);
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
        public async Task WarnUserAsync(SocketGuildUser user, float size, [Remainder] string reason) {
            _ = user.Warn(size, reason, Context);

            await ReplyAsync(user.Mention + " has gotten their " + user.LoadInfractions().Count.Suffix() + " infraction for " + reason);
        }

        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserSmallSizeAsync(SocketGuildUser user, [Remainder] string reason) {
            _ = user.Warn(1, reason, Context);

            await ReplyAsync(user.Mention + " has gotten their " + user.LoadInfractions().Count.Suffix() + " infraction for " + reason);
        }

        [Command("warns")]
        [RequireContext(ContextType.Guild)]
        [Alias("infractions", "warnings")]
        public async Task CheckUserWarnsAsync(SocketGuildUser user = null, int amount = 5) {
            if (user == null) {
                user = Context.Message.Author as SocketGuildUser;
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

                    await ReplyAsync("removed " + user.Mention + "'s warning for " + reason);
                }
            } else {
                await ReplyAsync(user.Username + " has no warns");
            }
        }

        [Command("kickwarn")]
        [Alias("warnkick")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn(SocketGuildUser user, [Remainder] string reason) {
            await user.Warn(1, reason, Context, "Discord");

            var embed = new EmbedBuilder();
            embed.WithTitle("You have been kicked from a discord guild");
            embed.AddField("Reason", reason, true);
            embed.AddField("Guild name", Context.Guild.Name, true);
            embed.WithCurrentTimestamp();
            embed.WithAuthor(Context.Message.Author);

            await user.GetOrCreateDMChannelAsync().Result.SendMessageAsync(embed: embed.Build());
            await ReplyAsync(user.Mention + " has been kicked for " + reason);
            await user.KickAsync(reason);
        }

        [Command("testtempban")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task TempBan(SocketUser user, int days, [Remainder] string reason) {
            string plural = "";
            if (days < 0) {
                await ReplyAsync("Can't warn for 0 days");
                return;
            }
            if (days > 1) {
                plural = "s ";
            }
            IUserMessage message = await ReplyAsync("Banning " + user.Mention + " for " + days + " day" + plural + " because of " + reason);
            TempBan tempBan = new TempBan(user.Id, days);
            List<TempBan> tempBans = Context.Guild.LoadTempActions(true);
            tempBans.Add(tempBan);
            tempBans.SaveTempBans(Context.Guild);
            await Context.Guild.AddBanAsync(user, reason: reason);
            _ = message.ModifyAsync(msg => msg.Content = "Banned " + user.Mention + " for " + days + " day" + plural + " because of " + reason);
            IDMChannel DM = await user.GetOrCreateDMChannelAsync();
            _ = DM.SendMessageAsync("You have been temp banned in " + Context.Guild.Name + " discord for \"" + reason + "\" for " + days + " day" + plural);
        }
    }

    public static class Filter {
        public static DiscordSocketClient client;
        public static async Task CheckMessage(SocketMessage message) {
            try {
                if (message.Author.IsBot && !(message.Channel is SocketGuildChannel)) {
                    return; //Makes sure it's not logging a message from a bot and that it's in a discord server
                }
                SocketCommandContext context = new SocketCommandContext(client, message as SocketUserMessage);
                var chnl = message.Channel as SocketGuildChannel;
                var Guild = chnl.Guild;
                string guildDir = Guild.GetPath();
                if (Guild != null && Directory.Exists(guildDir) && !Utilities.HasAdmin(message.Author as SocketGuildUser)) {
                    ModerationSettings modSettings = Guild.LoadModSettings(false);
                    List<BadWord> badWords = Guild.LoadBadWords();

                    if (modSettings != null) {
                        if (modSettings.channelsWithoutAutoMod != null && modSettings.channelsWithoutAutoMod.Contains(chnl.Id)) {
                            return; //Returns if channel is set as not using automod
                        }
                        //Checks if a message contains an invite
                        if (message.Content.ToLower().Contains("discord.gg/") || message.Content.ToLower().Contains("discordapp.com/invite/")) {
                            if (!modSettings.invitesAllowed) {
                                _ = ((SocketGuildUser)message.Author).Warn(0.5f, "Posted Invite", context);
                                await message.Channel.SendMessageAsync(message.Author.Mention + " has been given their " + (message.Author as SocketGuildUser).LoadInfractions("Discord").Count.Suffix() + " infraction because of posting a discord invite");

                                Logging.LogMessage("Bad word removed", message, Guild);
                                await message.DeleteAsync();
                                return;
                            }
                        }
                        if (modSettings.allowedLinks != null && modSettings.allowedLinks.Count > 0) {
                            const string linkRegex = @"/ ^((?: https ?| steam):\/\/[^\s <] +[^<.,:; \" + "\" '\\]\\s])/";
                            MatchCollection matches = Regex.Matches(message.Content, linkRegex, RegexOptions.IgnoreCase);
                            foreach (string match in matches) {
                                if (!modSettings.allowedLinks.Any(s => match.ToLower().Contains(s.ToLower()))) {
                                    await ((SocketGuildUser)message.Author).Warn(1, "Using unauthorized links", context);
                                }
                            }
                        }
                    }

                    if (File.Exists(guildDir + "/badwords.json")) {
                        foreach (BadWord badWord in badWords) {
                            if (message.Content.ToLower().Contains(badWord.word.ToLower())) {
                                if (badWord.euphemism != null && badWord.euphemism != "") {
                                    await ((SocketGuildUser)message.Author).Warn(0.5f, "Bad word used (" + badWord.euphemism + ")", context);
                                } else {
                                    await ((SocketGuildUser)message.Author).Warn(0.5f, "Bad word usage", context);
                                }
                                await message.Channel.SendMessageAsync(message.Author.Mention + " has been given their " + (message.Author as SocketGuildUser).LoadInfractions("Discord").Count.Suffix() + " infraction because of using a bad word");

                                Logging.LogMessage("Bad word removed", message, Guild);
                                await message.DeleteAsync();
                                return;
                            }
                        }
                    }
                }
            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "Filter", "Something went wrong with the filter", e).Log();
            }
        }
    }
}