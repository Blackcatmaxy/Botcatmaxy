using System.Collections.Generic;
using BotCatMaxy.Moderation;
using System.Threading.Tasks;
using Discord.WebSocket;
using BotCatMaxy.Data;
using Discord.Rest;
using System.Linq;
using Humanizer;
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

        public static async Task CheckTempActs(DiscordSocketClient client, bool debug = false) {
            RequestOptions requestOptions = RequestOptions.Default;
            requestOptions.RetryMode = RetryMode.AlwaysRetry;
            try {
                int checkedGuilds = 0;
                foreach (RestGuild guild in await client.Rest.GetGuildsAsync(requestOptions)) {
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
                                    RestBan ban = await guild.GetBanAsync(tempBan.user, requestOptions);
                                    if (ban == null) { //Need to add an embed for when this happens that's distinct
                                        _ = client.Rest.GetUserAsync(tempBan.user)?.TryNotify($"As you might know, you have been manually unbanned in {guild.Name} discord");
                                        //_ = new LogMessage(LogSeverity.Warning, "TempAction", "Tempbanned person isn't banned").Log();
                                        editedBans.Remove(tempBan);
                                    } else if (DateTime.Now >= tempBan.dateBanned.Add(tempBan.length)) {
                                        RestUser rUser = ban.User;
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
                        if (settings != null && guild.GetRole(settings.mutedRole) != null && actions.tempMutes.NotEmpty()) {
                            RestRole mutedRole = guild.GetRole(settings.mutedRole);
                            List<TempAct> editedMutes = new List<TempAct>(actions.tempMutes);
                            uint checkedMutes = 0;
                            foreach (TempAct tempMute in actions.tempMutes) {
                                checkedMutes++;
                                try {
                                    RestGuildUser gUser = await guild.GetUserAsync(tempMute.user, requestOptions);
                                    if (!gUser?.RoleIds.Contains(settings.mutedRole) ?? false) {
                                        _ = gUser.TryNotify($"As you might know, you have been manually unmuted in {guild.Name} discord");
                                        editedMutes.Remove(tempMute);
                                    } else if (DateTime.Now >= tempMute.dateBanned.Add(tempMute.length)) {
                                        if (gUser != null) {
                                            await gUser.RemoveRoleAsync(mutedRole, requestOptions);
                                        }
                                        if (!gUser?.RoleIds.Contains(settings.mutedRole) ?? false) { //Doesn't remove tempmute if unmuting fails
                                            RestUser user = gUser;
                                            user ??= await client.Rest.GetUserAsync(tempMute.user, requestOptions);
                                            if (gUser != null) {
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
                await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong checking temp actions", e).Log();
            }
        }

        public async Task Timer() {
            while (true) {
                DateTime start = DateTime.Now;
                await CheckTempActs(client);
                int delayMiliseconds = 30000 - start.Subtract(DateTime.Now).Milliseconds;
                if (delayMiliseconds < 0)
                    await new LogMessage(LogSeverity.Critical, "TempAct", "Temp actions took longer than 30 seconds to complete").Log();
                else
                    await Task.Delay(delayMiliseconds);
            }
        }
    }
}
