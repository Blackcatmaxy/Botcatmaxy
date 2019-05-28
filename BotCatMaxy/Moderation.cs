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
//using Discord.Addons.Preconditions;

namespace BotCatMaxy {
    [Serializable]
    public struct Infraction {
        public DateTime time;
        public string reason;
        public float size;
    }

    public static class ModerationFunctions {
        public static void CheckDirectories(this IGuild guild) {
            //old directory was fC:/Users/Daniel/Google-Drive/Botcatmaxy/Data/
            if (!Directory.Exists("/home/bob_the_daniel/Data/" + guild.OwnerId)) {
                Directory.CreateDirectory("/home/bob_the_daniel/Data/" + guild.OwnerId);
            }
            if (!Directory.Exists("/home/bob_the_daniel/Data/" + guild.OwnerId + "/Infractions/")) {
                Directory.CreateDirectory("/home/bob_the_daniel/Data/" + guild.OwnerId + "/Infractions/");
            }
            if (!Directory.Exists("/home/bob_the_daniel/Data/" + guild.OwnerId + "/Infractions/Discord/")) {
                Directory.CreateDirectory("/home/bob_the_daniel/Data/" + guild.OwnerId + "/Infractions/Discord/");
            }
            if (!Directory.Exists("/home/bob_the_daniel/Data/" + guild.OwnerId + "/Infractions/Games/")) {
                Directory.CreateDirectory("/home/bob_the_daniel/Data/" + guild.OwnerId + "/Infractions/Games/");
            }
        }

        public static void SaveInfractions(string location, List<Infraction> infractions) {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Create(location);
            bf.Serialize(file, infractions.ToArray());
            file.Close();
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
            SaveInfractions(context.Guild.GetPath(true) + "/Infractions/" + dir + "/" + user.Id, infractions);

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
                    size = "("  + infraction.size  + "x) ";
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

    public static class TempBanChecker {
        public static async Task Timer(DiscordSocketClient client) {
            foreach (SocketGuild guild in client.Guilds) {
                string guildDir = guild.GetPath(false);
                if (guildDir != null && Directory.Exists(guildDir) && File.Exists(guildDir + "tempActions.json")) {
                    List<TempBan> tempBans = guild.LoadTempActions(false);
                    if (tempBans != null && tempBans.Count > 0) {
                        foreach(TempBan tempBan in tempBans) {
                            bool needSave = false;
                            if (DateTime.Now.Subtract(tempBan.dateBanned).Days >= tempBan.length) {
                                if (client.GetUser(tempBan.personBanned) != null && await guild.GetBanAsync(tempBan.personBanned) != null) {
                                    await guild.RemoveBanAsync(tempBan.personBanned);
                                    tempBans.Remove(tempBan);
                                    needSave = true;
                                }
                            }
                            tempBans.SaveTempBans(guild);
                        }
                    }
                }
            }

            await Task.Delay(3600000);
            _ = Timer(client);
        }
    }

    [Group("games")]
    [Alias("game")]
    [RequireContext(ContextType.Guild)]
    public class GameWarnModule : ModuleBase<SocketCommandContext> {
        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserAsync(SocketGuildUser user, float size, [Remainder] string reason) {
            ModerationFunctions.CheckDirectories(Context.Guild);
            await user.Warn(size, reason, Context, "Games");

            await ReplyAsync(user.Username + " has been warned for " + reason);
        }

        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserSmallSizeAsync(SocketGuildUser user, [Remainder] string reason) {
            ModerationFunctions.CheckDirectories(Context.Guild);
            await user.Warn(1, reason, Context, "Games");

            await ReplyAsync(user.Username + " has been warned for " + reason);
        }

        [Command("warns")]
        [RequireContext(ContextType.Guild)]
        [Alias("infractions", "warnings")]
        public async Task CheckUserWarnsAsync(SocketGuildUser user = null, int amount = 5) {
            if (user == null) {
                user = Context.Message.Author as SocketGuildUser;
            }

            ModerationFunctions.CheckDirectories(Context.Guild);
            if (File.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/Infractions/Games/" + user.Id)) {
                await ReplyAsync(embed: user.CheckInfractions("Games", amount));
            } else {
                await ReplyAsync(user.Username + " has no warns");
            }
        }

        [Command("removewarn")]
        [Alias("warnremove", "removewarning")]
        [HasAdmin()]
        public async Task RemooveWarnAsync(SocketGuildUser user, int index) {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do have administrator permissions");
                return;
            }
            ModerationFunctions.CheckDirectories(Context.Guild);
            if (File.Exists(user.Guild.GetPath(false) + "/Infractions/Games/" + user.Id)) {
                List<Infraction> infractions = user.LoadInfractions();

                if (infractions.Count < index || index <= 0) {
                    await ReplyAsync("invalid infraction number");
                } else if (infractions.Count == 1) {
                    await ReplyAsync("removed " + user.Username + "'s warning for " + infractions[0]);
                    File.Delete("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/Infractions/Games/" + user.Id);
                } else {
                    string reason = infractions[index - 1].reason;
                    infractions.RemoveAt(index - 1);

                    ModerationFunctions.SaveInfractions(Context.Guild.OwnerId + "/Infractions/Games/" + user.Id, infractions);

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
            ModerationFunctions.CheckDirectories(Context.Guild);
            _ = user.Warn(size, reason, Context);

            await ReplyAsync(user.Username + " has been warned for " + reason);
        }

        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserSmallSizeAsync(SocketGuildUser user, [Remainder] string reason) {
            ModerationFunctions.CheckDirectories(Context.Guild);
            _ = user.Warn(1, reason, Context);

            await ReplyAsync(user.Username + " has been warned for " + reason);
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
            ModerationFunctions.CheckDirectories(Context.Guild);
            if (File.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/Infractions/Discord/" + user.Id)) {
                List<Infraction> infractions = user.LoadInfractions();

                if (infractions.Count < index || index <= 0) {
                    await ReplyAsync("invalid infraction number");
                } else if (infractions.Count == 1) {
                    await ReplyAsync("removed " + user.Username + "'s warning for " + infractions[index - 1].reason);
                    File.Delete("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/Infractions/Discord/" + user.Id);
                } else {
                    string reason = infractions[index - 1].reason;
                    infractions.RemoveAt(index - 1);

                    ModerationFunctions.SaveInfractions(Context.Guild.OwnerId + "/Infractions/Discord/" + user.Id, infractions);

                    await ReplyAsync("removed " + user.Mention + "'s warning for " + reason);
                }
            } else {
                await ReplyAsync(user.Username + " has no warns");
            }
        }

        [Command("testtempban")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task TempBan(SocketUser user, int days, [Remainder] string reason) {
            string plural = "";
            if (days == 0) {
                await ReplyAsync("Can't warn for 0 days");
                return;
            }
            if (days > 1) {
                plural = "s ";
            }
            IUserMessage message = await ReplyAsync("Banning " + user.Mention + " for " + days + " day " + plural + "because of " + reason);
            TempBan tempBan = new TempBan(user.Id, days);
            List<TempBan> tempBans = Context.Guild.LoadTempActions(true);
            tempBans.Add(tempBan);
            tempBans.SaveTempBans(Context.Guild);
            await Context.Guild.AddBanAsync(user, reason: reason);
            _ = message.ModifyAsync(msg => msg.Content = "Banned " + user.Mention + " for " + days + " day " + plural + "because of " + reason);
            IDMChannel DM = await user.GetOrCreateDMChannelAsync();
            _ = DM.SendMessageAsync("You have been temp banned in " + Context.Guild.Name + " discord for \"" + reason + "\" for " + days + " days");
        }
    }

    public static class Filter {
        public static DiscordSocketClient client;
        public static async Task CheckMessage(SocketMessage message) {
            if (message.Author.IsBot && !(message.Channel is SocketGuildChannel)) {
                return; //Makes sure it's not logging a message from a bot and that it's in a discord server
            }
            SocketCommandContext context = new SocketCommandContext(client, message as SocketUserMessage);
            var chnl = message.Channel as SocketGuildChannel;
            var Guild = chnl.Guild;
            if (Guild != null && Directory.Exists("/home/bob_the_daniel/Data/" + Guild.OwnerId) && !Utilities.HasAdmin(message.Author as SocketGuildUser)) {
                ModerationSettings modSettings = Guild.LoadModSettings(false);
                List<BadWord> badWords = Guild.LoadBadWords();
                
                if (modSettings != null) {
                    if (modSettings.channelsWithoutAutoMod.Contains(chnl.Id)) {
                        return; //Returns if channel is set as not using automod
                    }
                    //Checks if a message contains an invite
                    if (message.Content.Contains("discord.gg/")) {
                        if (!modSettings.invitesAllowed) {
                            _ = ((SocketGuildUser)message.Author).Warn(0.5f, "Posted Invite", context);
                            await message.Channel.SendMessageAsync("warned " + message.Author.Mention + " for posting a discord invite");

                            Logging.LogDeleted("Bad word removed", message, Guild);
                            await message.DeleteAsync();
                            return;
                        }
                    }
                } 

                if (File.Exists("/home/bob_the_daniel/Data/" + Guild.OwnerId + "/badwords.json")) {
                    foreach (BadWord badWord in badWords) {
                        if (message.Content.Contains(badWord.word)) {
                            if (badWord.euphemism != null && badWord.euphemism != "") {
                                _ = ((SocketGuildUser)message.Author).Warn(0.5f, "Bad word used (" + badWord.euphemism + ")", context);
                            } else {
                                _ = ((SocketGuildUser)message.Author).Warn(0.5f, "Bad word usage", context);
                            }
                            await message.Channel.SendMessageAsync("warned " + message.Author.Mention + " for bad word");
                            
                            Logging.LogDeleted("Bad word removed", message, Guild);
                            await message.DeleteAsync();
                            return;
                        }
                    }
                }
            }
        }
    }
}