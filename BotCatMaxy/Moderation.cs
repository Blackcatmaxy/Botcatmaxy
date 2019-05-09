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
            FileStream file = File.Create("/home/bob_the_daniel/Data/" + location);
            bf.Serialize(file, infractions.ToArray());
            file.Close();
        }

        public static List<Infraction> LoadInfractions(string location) {
            List<Infraction> infractions = new List<Infraction>();

            if (File.Exists("/home/bob_the_daniel/Data/" + location)) {

                BinaryFormatter newbf = new BinaryFormatter();
                FileStream newFile = File.Open("/home/bob_the_daniel/Data/" + location, FileMode.Open);
                Infraction[] oldInfractions;
                oldInfractions = (Infraction[])newbf.Deserialize(newFile);
                newFile.Close();
                foreach (Infraction infraction in oldInfractions) {
                    infractions.Add(infraction);
                }
            }
            return infractions;
        }

        public static async Task Warn(this IUser user, float size, string reason, SocketCommandContext context, string dir = "Discord") {
            if (size > 999 || size < 0.01) {
                await context.Channel.SendMessageAsync("Why would you need to warn someone with that size?");
                return;
            }

            List<Infraction> infractions = LoadInfractions(context.Guild.OwnerId + "/Infractions/" + dir + "/" + user.Id);

            Infraction newInfraction = new Infraction {
                reason = reason,
                time = DateTime.Now,
                size = size
            };
            infractions.Add(newInfraction);
            SaveInfractions(context.Guild.OwnerId + "/Infractions/" + dir + "/" + user.Id, infractions);

            IUser[] users = await context.Channel.GetUsersAsync().Flatten().ToArray();
            if (!users.Contains(user)) {
                IDMChannel DM = await user.GetOrCreateDMChannelAsync();
                _ = DM.SendMessageAsync("You have been warned in " + context.Guild.Name + "discord for \"" + reason + "\" in a channel you can't view");
            }
        }

        public static Embed CheckInfractions(SocketUser user, string location) {
            List<Infraction> infractions = LoadInfractions(location);

            string infractionList = "";
            float infractionsToday = 0;
            float infractions30Days = 0;
            float totalInfractions = 0;
            float last7Days = 0;
            string plural = "";
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
                        if (dateAgo.Days == 1) {
                            plural = "";
                        } else {
                            plural = "s";
                        }
                        timeAgo = dateAgo.Hours + " hour" + plural + " ago";
                        if (dateAgo.Hours < 1) {
                            if (dateAgo.Hours == 1) {
                                plural = "";
                            } else {
                                plural = "s";
                            }
                            timeAgo = dateAgo.Minutes + " minute" + plural + " ago";
                            if (dateAgo.Seconds < 1) {
                                if (dateAgo.TotalSeconds == 1) {
                                    plural = "";
                                } else {
                                    plural = "s";
                                }
                                timeAgo = dateAgo.TotalSeconds + " second" + plural + " ago";
                            }
                        }
                    }
                }

                string size = "";
                if (infraction.size != 1) {
                    size = "["  + infraction.size  + "x] ";
                }

                infractionList += size + infraction.reason + " - " + timeAgo;
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
                string guildDir = guild.GuildDataPath(false);
                if (guildDir != null) {

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
        public async Task WarnUserAsync(SocketUser user, float size, [Remainder] string reason) {
            ModerationFunctions.CheckDirectories(Context.Guild);
            await user.Warn(size, reason, Context, "Games");

            await ReplyAsync(user.Username + " has been warned for " + reason);
        }

        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserSmallSizeAsync(SocketUser user, [Remainder] string reason) {
            ModerationFunctions.CheckDirectories(Context.Guild);
            await user.Warn(1, reason, Context, "Games");

            await ReplyAsync(user.Username + " has been warned for " + reason);
        }

        [Command("warns")]
        [Alias("infractions", "warnings")]
        public async Task CheckUserWarnsAsync(SocketUser user = null) {
            if (user == null) {
                user = Context.Message.Author;
            }

            ModerationFunctions.CheckDirectories(Context.Guild);
            if (File.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/Infractions/Games/" + user.Id)) {
                await ReplyAsync(embed: ModerationFunctions.CheckInfractions(user, Context.Guild.OwnerId + "/Infractions/Games/" + user.Id));
            } else {
                await ReplyAsync(user.Username + " has no warns");
            }
        }

        [Command("removewarn")]
        [Alias("warnremove", "removewarning")]
        [HasAdmin()]
        public async Task RemooveWarnAsync(SocketUser user, int index) {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do have administrator permissions");
                return;
            }
            ModerationFunctions.CheckDirectories(Context.Guild);
            if (File.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/Infractions/Games/" + user.Id)) {
                List<Infraction> infractions = ModerationFunctions.LoadInfractions(Context.Guild.OwnerId + "/Infractions/Games/" + user.Id);

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
            ModerationSettings settings = SettingFunctions.LoadModSettings(Context.Guild, false);
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
        public async Task WarnUserAsync(SocketUser user, float size, [Remainder] string reason) {
            ModerationFunctions.CheckDirectories(Context.Guild);
            _ = user.Warn(size, reason, Context);

            await ReplyAsync(user.Username + " has been warned for " + reason);
        }

        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserSmallSizeAsync(SocketUser user, [Remainder] string reason) {
            ModerationFunctions.CheckDirectories(Context.Guild);
            _ = user.Warn(1, reason, Context);

            await ReplyAsync(user.Username + " has been warned for " + reason);
        }

        [Command("warns")]
        [Alias("infractions", "warnings")]
        public async Task CheckUserWarnsAsync(SocketUser user = null) {
            ModerationFunctions.CheckDirectories(Context.Guild);
            if (user == null) {
                user = Context.Message.Author;
            }
            if (File.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/Infractions/Discord/" + user.Id)) {
                await ReplyAsync(embed: ModerationFunctions.CheckInfractions(user, Context.Guild.OwnerId + "/Infractions/Discord/" + user.Id));
            } else {
                await ReplyAsync(user.Username + " has no warns");
            }
        }

        [Command("removewarn")]
        [Alias("warnremove", "removewarning")]
        [HasAdmin()]
        public async Task RemoveWarnAsync(SocketUser user, int index) {
            ModerationFunctions.CheckDirectories(Context.Guild);
            if (File.Exists("/home/bob_the_daniel/Data/" + Context.Guild.OwnerId + "/Infractions/Discord/" + user.Id)) {
                List<Infraction> infractions = ModerationFunctions.LoadInfractions(Context.Guild.OwnerId + "/Infractions/Discord/" + user.Id);

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
                            _ = message.Author.Warn(0.5f, "Posted Invite", context);
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
                                _ = message.Author.Warn(0.5f, "Bad word used (" + badWord.euphemism + ")", context);
                            } else {
                                _ = message.Author.Warn(0.5f, "Bad word usage", context);
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