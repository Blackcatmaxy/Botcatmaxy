using System.Collections.Generic;
using BotCatMaxy.Moderation;
using System.Threading.Tasks;
using Discord.WebSocket;
using BotCatMaxy.Data;
using Discord.Rest;
using System.Linq;
using Discord;
using System;

namespace BotCatMaxy {
    public class TempActions {
        readonly DiscordSocketClient client;
        public TempActions(DiscordSocketClient client) {
            this.client = client;
            client.Ready += Ready;
            client.UserJoined += CheckNewUser;
        }

        public async Task Ready() {
            client.Ready -= Ready;
            _ = Task.Run(() => Timer());
        }

        async Task CheckNewUser(SocketGuildUser user) {
            ModerationSettings settings = user.Guild?.LoadFromFile<ModerationSettings>();
            TempActionList actions = user.Guild?.LoadFromFile<TempActionList>();
            if (settings == null || user.Guild?.GetRole(settings.mutedRole) == null || (actions?.tempMutes?.IsNullOrEmpty() ?? true)) return;
            if (actions.tempMutes.Any(tempMute => tempMute.user == user.Id)) _ = user.AddRoleAsync(user.Guild.GetRole(settings.mutedRole));
        }

        public static async Task TempActChecker(DiscordSocketClient client, bool debug = false) {
            try {
                int checkedGuilds = 0;
                foreach (RestGuild guild in await client.Rest.GetGuildsAsync()) {
                    if (debug) {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"\nChecking {guild.Name} discord ");
                    }
                    TempActionList actions = guild.LoadFromFile<TempActionList>(false);
                    bool needSave = false;
                    checkedGuilds++;
                    if (actions != null) {
                        if (!actions.tempBans.IsNullOrEmpty()) {
                            List<TempAct> editedBans = new List<TempAct>(actions.tempBans);
                            foreach (TempAct tempBan in actions.tempBans) {
                                try {
                                    if (!guild.GetBansAsync().Result.Any(ban => ban.User.Id == tempBan.user)) { //Need to add an embed for when this happens that's distinct
                                        _ = client.GetUser(tempBan.user)?.TryNotify($"As you might know, you have been manually unbanned in {guild.Name} discord");
                                        //_ = new LogMessage(LogSeverity.Warning, "TempAction", "Tempbanned person isn't banned").Log();
                                        editedBans.Remove(tempBan);
                                    } else if (DateTime.Now >= tempBan.dateBanned.Add(tempBan.length)) {
                                        RestUser rUser = guild.GetBansAsync().Result.First(ban => ban.User.Id == tempBan.user).User;
                                        await guild.RemoveBanAsync(tempBan.user);
                                        editedBans.Remove(tempBan);
                                        Logging.LogEndTempAct(guild, rUser, "bann", tempBan.reason, tempBan.length);
                                    }
                                } catch (Exception e) {
                                    _ = new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong unbanning someone, continuing", e).Log();
                                }
                            }

                            if (editedBans != actions.tempBans) {
                                if (debug) Console.Write($"{actions.tempBans.Count - editedBans.Count} tempbans are over, ");
                                needSave = true;
                                actions.tempBans = editedBans;
                            } else if (debug) Console.Write($"tempbans checked, none over, ");
                        } else if (debug) Console.Write($"no tempbans, ");

                        ModerationSettings settings = guild.LoadFromFile<ModerationSettings>();
                        if (settings != null && guild.GetRole(settings.mutedRole) != null && !actions.tempMutes.IsNullOrEmpty()) {
                            List<TempAct> editedMutes = new List<TempAct>(actions.tempMutes);
                            uint checkedMutes = 0;
                            foreach (TempAct tempMute in actions.tempMutes) {
                                checkedMutes++;
                                try {
                                    RestUser user = await guild.GetUserAsync(tempMute.user);
                                    if (user != null && !(user as IGuildUser).RoleIds.Contains(settings.mutedRole)) {
                                        _ = user.TryNotify($"As you might know, you have been manually unmuted in {guild.Name} discord");
                                        editedMutes.Remove(tempMute);
                                    } else if (DateTime.Now >= tempMute.dateBanned.Add(tempMute.length)) {
                                        if (user != null) {
                                            await guild.GetUserAsync(tempMute.user).Result.RemoveRoleAsync(guild.GetRole(settings.mutedRole));
                                        }
                                        if (!(user as IGuildUser)?.RoleIds?.NotEmpty() ?? true || !(user as IGuildUser).RoleIds.Contains(settings.mutedRole)) { //Doesn't remove tempmute if unmuting fails
                                            user ??= await client.Rest.GetUserAsync(tempMute.user);
                                            if (user != null) {
                                                Logging.LogEndTempAct(guild, user, "mut", tempMute.reason, tempMute.length);
                                                _ = user.Notify($"untemp-muted", tempMute.reason, guild);
                                            }
                                            editedMutes.Remove(tempMute);
                                        }
                                    }
                                } catch (Exception e) {
                                    _ = new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong unmuting someone, continuing", e).Log();
                                }
                            }

                            _ = (checkedMutes == actions.tempMutes.Count || checkedMutes == uint.MaxValue).AssertAsync("Didn't check all tempmutes");

                            if (editedMutes != actions.tempMutes) {
                                if (debug) Console.Write($"{actions.tempMutes.Count - editedMutes.Count}/{actions.tempMutes.Count} tempmutes are over");
                                actions.tempMutes = editedMutes;
                                needSave = true;
                            } else if (debug) Console.Write($"no tempmute changes");
                        } else if (debug) Console.Write("no tempmutes to check or no settings");
                        if (needSave) actions.SaveToFile();
                    }
                }
                if (debug) Console.Write("\n");
                _ = (checkedGuilds > 0).AssertWarnAsync("Checked 0 guilds for tempbans?");

            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong unbanning someone", e).Log();
            }
        }

        public async Task Timer() {
            _ = TempActChecker(client);

            await Task.Delay(60000);
            _ = Timer();
        }
    }
}
