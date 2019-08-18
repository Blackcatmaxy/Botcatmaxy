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
            string guildDir = user.Guild.GetPath(false);
            if (guildDir == null || !Directory.Exists(guildDir) || !File.Exists(guildDir + "/tempMutes.json")) return;
            ModerationSettings settings = user.Guild.LoadFromFile<ModerationSettings>("moderationSettings.txt");
            if (settings == null || user.Guild.GetRole(settings.mutedRole) == null) return;
            if (user.Guild.LoadFromFile<List<TempAct>>("tempMutes.json").Any(tempMute => tempMute.user == user.Id)) _ = user.AddRoleAsync(user.Guild.GetRole(settings.mutedRole));
        }

        public static async Task TempActChecker(DiscordSocketClient client) {
            try {
                int checkedGuilds = 0;
                foreach (SocketGuild guild in client.Guilds) {
                    string guildDir = guild.GetPath(false);
                    checkedGuilds++;
                    if (guildDir != null && Directory.Exists(guildDir) && File.Exists(guildDir + "/tempBans.json")) {
                        List<TempAct> tempBans = guild.LoadFromFile<List<TempAct>>("tempBans.json");
                        List<TempAct> editedBans = new List<TempAct>(tempBans);
                        if (tempBans != null && tempBans.Count > 0) {
                            foreach (TempAct tempBan in tempBans) {
                                try {
                                    if (client.GetUser(tempBan.user) == null) {
                                        _ = new LogMessage(LogSeverity.Warning, "TempAction", "User is null").Log();
                                        editedBans.Remove(tempBan);
                                    } else if (!guild.GetBansAsync().Result.Any(ban => ban.User.Id == tempBan.user)) { //Need to add an embed for when this happens that's distinct
                                        _ = new LogMessage(LogSeverity.Warning, "TempAction", "Tempbanned person isn't banned").Log();
                                        editedBans.Remove(tempBan);
                                    } else if (DateTime.Now >= tempBan.dateBanned.Add(tempBan.length)) {
                                        await guild.RemoveBanAsync(tempBan.user);
                                        editedBans.Remove(tempBan);
                                        Logging.LogEndTempAct(guild, guild.GetUser(tempBan.user), "ban", tempBan.reason, tempBan.length);
                                    }
                                } catch (Exception e) {
                                    _ = new LogMessage(LogSeverity.Error, "TempAction", "Something went wrong unbanning someone, continuing", e).Log();
                                }
                            }

                            if (editedBans != tempBans) {
                                editedBans.SaveToFile("tempBans.json", guild);
                            }
                        }

                        ModerationSettings settings = guild.LoadFromFile<ModerationSettings>("moderationSettings.txt");
                        if (settings != null && guild.GetRole(settings.mutedRole) != null) {
                            List<TempAct> tempMutes = guild.LoadFromFile<List<TempAct>>("tempMutes.json");
                            List<TempAct> editedMutes = new List<TempAct>(tempMutes);
                            if (tempMutes != null && tempMutes.Count > 0) {
                                foreach (TempAct tempMute in tempMutes) {
                                    try {
                                        if (DateTime.Now >= tempMute.dateBanned.Add(tempMute.length)) {
                                            SocketGuildUser user = guild.GetUser(tempMute.user);
                                            if (user != null) {
                                                await guild.GetUser(tempMute.user).RemoveRoleAsync(guild.GetRole(settings.mutedRole));
                                                _ = user.Notify($"untemp-muted", tempMute.reason, guild);
                                            }
                                            editedMutes.Remove(tempMute);
                                            Logging.LogEndTempAct(guild, guild.GetUser(tempMute.user), "mut", tempMute.reason, tempMute.length);
                                        }
                                    } catch (Exception e) {
                                        _ = new LogMessage(LogSeverity.Error, "TempAction", "Something went wrong unmuting someone, continuing", e).Log();
                                    }
                                }

                                if (editedMutes != tempMutes) {
                                    editedMutes.SaveToFile("tempMutes.json", guild);
                                }
                            }
                        }
                    }
                }
                _ = (checkedGuilds > 0).AssertWarnAsync("Checked 0 guilds for tempbans?");

            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "TempAction", "Something went wrong unbanning someone", e).Log();
            }
        }

        public async Task Timer() {
            _ = TempActChecker(client);

            await Task.Delay(600000);
            _ = Timer();
        }
    }
}
