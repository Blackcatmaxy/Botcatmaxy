using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord.WebSocket;
using BotCatMaxy.Data;
using BotCatMaxy;
using System.Text;
using Discord;
using System.IO;
using Discord.Rest;
using BotCatMaxy.Settings;
using System.Linq;

namespace BotCatMaxy {
    public class TempActions {
        readonly DiscordSocketClient client;
        public TempActions(DiscordSocketClient client) {
            this.client = client;
            client.Ready += Ready;
            client.UserJoined += CheckNewUser;
        }

        public async Task Ready() {
            _ = Timer();
        }

        async Task CheckNewUser(SocketGuildUser user) {
            var guildDir = user.Guild.GetCollection(false);
            if (guildDir == null) return;
            ModerationSettings settings = user.Guild.LoadFromFile<ModerationSettings>();
            if (settings == null || user.Guild.GetRole(settings.mutedRole) == null) return;
            if (user.Guild.LoadFromFile<List<TempAct>>().Any(tempMute => tempMute.user == user.Id)) _ = user.AddRoleAsync(user.Guild.GetRole(settings.mutedRole));
        }

        public static async Task TempActChecker(DiscordSocketClient client, bool debug = false) {
            try {
                int checkedGuilds = 0;
                foreach (SocketGuild guild in client.Guilds) {
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
                                        _ = new LogMessage(LogSeverity.Warning, "TempAction", "Tempbanned person isn't banned").Log();
                                        editedBans.Remove(tempBan);
                                    } else if (DateTime.Now >= tempBan.dateBanned.Add(tempBan.length)) {
                                        RestUser rUser = guild.GetBansAsync().Result.First(ban => ban.User.Id == tempBan.user).User;
                                        await guild.RemoveBanAsync(tempBan.user);
                                        editedBans.Remove(tempBan);
                                        Logging.LogEndTempAct(guild, rUser, "ban", tempBan.reason, tempBan.length);
                                    }
                                } catch (Exception e) {
                                    _ = new LogMessage(LogSeverity.Error, "TempAction", "Something went wrong unbanning someone, continuing", e).Log();
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
                                    if (DateTime.Now >= tempMute.dateBanned.Add(tempMute.length)) {
                                        SocketUser user = guild.GetUser(tempMute.user);
                                        if (user != null) {
                                            await (user as SocketGuildUser).RemoveRoleAsync(guild.GetRole(settings.mutedRole));
                                        }
                                        user = client.GetUser(tempMute.user);
                                        if (user != null) {
                                            Logging.LogEndTempAct(guild, user, "mut", tempMute.reason, tempMute.length);
                                            _ = user.Notify($"untemp-muted", tempMute.reason, guild);
                                        }
                                        editedMutes.Remove(tempMute);
                                    }
                                } catch (Exception e) {
                                    _ = new LogMessage(LogSeverity.Error, "TempAction", "Something went wrong unmuting someone, continuing", e).Log();
                                }
                            }

                            _ = (checkedMutes == actions.tempMutes.Count || checkedMutes == uint.MaxValue).AssertAsync("Didn't check all tempmutes");

                            if (editedMutes != actions.tempMutes) {
                                if (debug) Console.Write($"{actions.tempMutes.Count - editedMutes.Count} tempmutes are over");
                                actions.tempMutes = editedMutes;
                                needSave = true;
                            } else if (debug) Console.Write($"no tempmute changes");
                        } else if (debug) Console.Write("no tempmutes to check or no settings");
                        if (needSave) actions.SaveToFile(guild);
                    }
                }
                _ = (checkedGuilds > 0).AssertWarnAsync("Checked 0 guilds for tempbans?");

            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "TempAction", "Something went wrong unbanning someone", e).Log();
            }
        }

        public async Task Timer() {
            _ = TempActChecker(client);

            await Task.Delay(60000);
            _ = Timer();
        }
    }
}
