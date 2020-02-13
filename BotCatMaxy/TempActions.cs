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
using System.Threading;

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
                foreach (SocketGuild sockGuild in client.Guilds) {
                    RestGuild restGuild = await client.Rest.GetGuildAsync(sockGuild.Id);
                    if (debug) {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"\nChecking {sockGuild.Name} discord ");
                    }
                    TempActionList actions = sockGuild.LoadFromFile<TempActionList>(false);
                    bool needSave = false;
                    checkedGuilds++;
                    if (actions != null) {
                        if (!actions.tempBans.IsNullOrEmpty()) {
                            List<TempAct> editedBans = new List<TempAct>(actions.tempBans);
                            foreach (TempAct tempBan in actions.tempBans) {
                                try {
                                    RestBan ban = (await sockGuild.GetBansAsync(requestOptions)).FirstOrDefault(tBan => tBan.User.Id == tempBan.user);
                                    if (ban == null) { //If manual unban
                                        var user = client.Rest.GetUserAsync(tempBan.user);
                                        editedBans.Remove(tempBan);
                                        _ = client.Rest.GetUserAsync(tempBan.user)?.TryNotify($"As you might know, you have been manually unbanned in {sockGuild.Name} discord");
                                        //_ = new LogMessage(LogSeverity.Warning, "TempAction", "Tempbanned person isn't banned").Log();
                                        await user;
                                        if (user.Result == null) {
                                            Logging.LogManualEndTempAct(sockGuild, tempBan.user, "bann", tempBan.dateBanned);
                                        } else {
                                            Logging.LogManualEndTempAct(sockGuild, user.Result, "bann", tempBan.dateBanned);
                                        }
                                    } else if (DateTime.Now >= tempBan.dateBanned.Add(tempBan.length)) {
                                        RestUser rUser = ban.User;
                                        await restGuild.RemoveBanAsync(tempBan.user, requestOptions);
                                        editedBans.Remove(tempBan);
                                        Logging.LogEndTempAct(sockGuild, rUser, "bann", tempBan.reason, tempBan.length);
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

                        ModerationSettings settings = sockGuild.LoadFromFile<ModerationSettings>();
                        if (settings != null && sockGuild.GetRole(settings.mutedRole) != null && actions.tempMutes.NotEmpty()) {
                            RestRole mutedRole = restGuild.GetRole(settings.mutedRole);
                            List<TempAct> editedMutes = new List<TempAct>(actions.tempMutes);
                            uint checkedMutes = 0;
                            foreach (TempAct tempMute in actions.tempMutes) {
                                checkedMutes++;
                                try {
                                    IGuildUser gUser = sockGuild.GetUser(tempMute.user);
                                    gUser ??= await restGuild.GetUserAsync(tempMute.user, requestOptions);
                                    if (gUser != null && !gUser.RoleIds.Contains(settings.mutedRole)) { //User missing muted role, must have been manually unmuted
                                        _ = gUser.TryNotify($"As you might know, you have been manually unmuted in {sockGuild.Name} discord");
                                        editedMutes.Remove(tempMute);
                                        Logging.LogManualEndTempAct(sockGuild, gUser, "mut", tempMute.dateBanned);
                                    } else if (DateTime.Now >= tempMute.dateBanned.Add(tempMute.length)) { //Normal mute end
                                        if (gUser != null) {
                                            await gUser.RemoveRoleAsync(mutedRole, requestOptions);
                                            gUser = sockGuild.GetUser(tempMute.user); //Reload user so it doesn't think user is missing role?
                                            gUser ??= await restGuild.GetUserAsync(tempMute.user, requestOptions);
                                        } // if user not in guild || if user doesn't contain muted role (successfully removed?
                                        if (gUser == null || !gUser.RoleIds.Contains(settings.mutedRole)) { //Doesn't remove tempmute if unmuting fails
                                            IUser user = gUser; //Gets user to try to message
                                            user ??= await client.Rest.GetUserAsync(tempMute.user, requestOptions);
                                            if (user != null) { // if possible to message, message and log
                                                Logging.LogEndTempAct(sockGuild, user, "mut", tempMute.reason, tempMute.length);
                                                _ = user.Notify($"untemp-muted", tempMute.reason, sockGuild);
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
                var cts = new CancellationTokenSource();
                DateTime start = DateTime.Now;
                _ = Task.Run(() => TimeWarning(start, cts.Token));
                await CheckTempActs(client);
                cts.Cancel();
                int delayMiliseconds = 30000 - DateTime.Now.Subtract(start).Milliseconds;
                if (delayMiliseconds < 0)
                    await new LogMessage(LogSeverity.Critical, "TempAct", "Temp actions took longer than 30 seconds to complete").Log();
                else
                    await Task.Delay(delayMiliseconds);
                cts.Dispose();
            }
        }

        public async Task TimeWarning(DateTime start, CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                if (DateTime.Now.Subtract(start).Milliseconds > 30000)
                    await new LogMessage(LogSeverity.Critical, "TempAct", "Temp actions took longer than 30 seconds to complete and still haven't canceled").Log();
                await Task.Delay(100);
            }
        }
    }
}
