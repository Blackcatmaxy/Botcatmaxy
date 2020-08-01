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
using BotCatMaxy.Models;
using BotCatMaxy.Components.Logging;

namespace BotCatMaxy
{
    public class TempActions
    {
        readonly DiscordSocketClient client;
        public static CurrentTempActionInfo currentInfo = new CurrentTempActionInfo();
        public static CachedTempActionInfo cachedInfo = new CachedTempActionInfo();
        public Timer timer;

        public TempActions(DiscordSocketClient client)
        {
            this.client = client;
            client.Ready += Ready;
            client.UserJoined += CheckNewUser;
        }

        public async Task Ready()
        {
            client.Ready -= Ready;
            timer = new Timer((_) => _ = ActCheckExec());
            timer.Change(0, 30000);
        }

        async Task CheckNewUser(SocketGuildUser user)
        {
            ModerationSettings settings = user.Guild?.LoadFromFile<ModerationSettings>();
            TempActionList actions = user.Guild?.LoadFromFile<TempActionList>();
            if (settings == null || user.Guild?.GetRole(settings.mutedRole) == null || (actions?.tempMutes?.IsNullOrEmpty() ?? true)) return;
            if (actions.tempMutes.Any(tempMute => tempMute.user == user.Id)) _ = user.AddRoleAsync(user.Guild.GetRole(settings.mutedRole));
        }

        public static async Task CheckTempActs(DiscordSocketClient client, bool debug = false)
        {
            RequestOptions requestOptions = RequestOptions.Default;
            requestOptions.RetryMode = RetryMode.AlwaysRetry;
            try
            {
                currentInfo.checkedGuilds = 0;
                foreach (SocketGuild sockGuild in client.Guilds)
                {
                    currentInfo.checkedMutes = 0;
                    if (currentInfo.checkedGuilds > client.Guilds.Count)
                    {
                        await new LogMessage(LogSeverity.Error, "TempAct", $"Check went past all guilds (at #{currentInfo.checkedGuilds}) but has been stopped. This doesn't seem physically possible.").Log();
                        return;
                    }
                    RestGuild restGuild = await client.Rest.SuperGetRestGuild(sockGuild.Id);
                    if (debug)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"\nChecking {sockGuild.Name} discord ");
                    }
                    TempActionList actions = sockGuild.LoadFromFile<TempActionList>(false);
                    bool needSave = false;
                    currentInfo.checkedGuilds++;
                    if (actions != null)
                    {
                        if (!actions.tempBans.IsNullOrEmpty())
                        {
                            var bans = await sockGuild.GetBansAsync(requestOptions);
                            currentInfo.editedBans = new List<TempAct>(actions.tempBans);
                            foreach (TempAct tempBan in actions.tempBans)
                            {
                                try
                                {
                                    RestBan ban = bans.FirstOrDefault(tBan => tBan.User.Id == tempBan.user);
                                    if (ban == null && bans != null)
                                    { //If manual unban
                                        var user = await client.Rest.GetUserAsync(tempBan.user);
                                        currentInfo.editedBans.Remove(tempBan);
                                        user?.TryNotify($"As you might know, you have been manually unbanned in {sockGuild.Name} discord");
                                        //_ = new LogMessage(LogSeverity.Warning, "TempAction", "Tempbanned person isn't banned").Log();
                                        if (user == null)
                                        {
                                            DiscordLogging.LogManualEndTempAct(sockGuild, tempBan.user, "bann", tempBan.dateBanned);
                                        }
                                        else
                                        {
                                            DiscordLogging.LogManualEndTempAct(sockGuild, user, "bann", tempBan.dateBanned);
                                        }
                                    }
                                    else if (DateTime.UtcNow >= tempBan.dateBanned.Add(tempBan.length))
                                    {
                                        RestUser rUser = ban.User;
                                        await restGuild.RemoveBanAsync(tempBan.user, requestOptions);
                                        currentInfo.editedBans.Remove(tempBan);
                                        DiscordLogging.LogEndTempAct(sockGuild, rUser, "bann", tempBan.reason, tempBan.length);
                                    }
                                }
                                catch (Exception e)
                                {
                                    await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong unbanning someone, continuing", e).Log();
                                }
                            }

                            //if all tempbans DON'T equal all edited tempbans (basically if there was a change
                            if (currentInfo.editedBans.Count != actions.tempBans.Count)
                            {
                                if (debug) Console.Write($"{actions.tempBans.Count - currentInfo.editedBans.Count} tempbans are over, ");
                                needSave = true;
                                actions.tempBans = currentInfo.editedBans;
                            }
                            else if (debug) Console.Write($"tempbans checked, none over, ");
                        }
                        else if (debug) Console.Write($"no tempbans, ");
                        ModerationSettings settings = sockGuild.LoadFromFile<ModerationSettings>();
                        if (settings != null && sockGuild.GetRole(settings.mutedRole) != null && actions.tempMutes.NotEmpty())
                        {
                            RestRole mutedRole = restGuild.GetRole(settings.mutedRole);
                            List<TempAct> editedMutes = new List<TempAct>(actions.tempMutes);
                            foreach (TempAct tempMute in actions.tempMutes)
                            {
                                currentInfo.checkedMutes++;
                                try
                                {
                                    IGuildUser gUser = await client.SuperGetUser(sockGuild, tempMute.user);
                                    if (gUser != null && !gUser.RoleIds.Contains(settings.mutedRole))
                                    { //User missing muted role, must have been manually unmuted
                                        _ = gUser.TryNotify($"As you might know, you have been manually unmuted in {sockGuild.Name} discord");
                                        editedMutes.Remove(tempMute);
                                        DiscordLogging.LogManualEndTempAct(sockGuild, gUser, "mut", tempMute.dateBanned);
                                        _ = (!editedMutes.Contains(tempMute)).AssertAsync("Tempmute not removed?!");
                                    }
                                    else if (DateTime.UtcNow >= tempMute.dateBanned.Add(tempMute.length))
                                    { //Normal mute end
                                        if (gUser != null)
                                        {
                                            await gUser.RemoveRoleAsync(mutedRole, requestOptions);
                                        } // if user not in guild || if user doesn't contain muted role (successfully removed?
                                        if (gUser == null || !gUser.RoleIds.Contains(settings.mutedRole))
                                        { //Doesn't remove tempmute if unmuting fails
                                            IUser user = gUser; //Gets user to try to message
                                            user ??= await client.SuperGetUser(tempMute.user);
                                            if (user != null)
                                            { // if possible to message, message and log
                                                DiscordLogging.LogEndTempAct(sockGuild, user, "mut", tempMute.reason, tempMute.length);
                                                _ = user.Notify($"untemp-muted", tempMute.reason, sockGuild);
                                            }
                                            editedMutes.Remove(tempMute);
                                        }
                                        else if (gUser != null) await new LogMessage(LogSeverity.Warning, "TempAct", "User should've had role removed").Log();
                                    }
                                }
                                catch (Exception e)
                                {
                                    await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong unmuting someone, continuing", e).Log();
                                }
                            }

                            //NOTE: Assertions fail if NOT true
                            _ = (currentInfo.checkedMutes == actions.tempMutes.Count).AssertAsync($"Checked incorrect number tempmutes ({currentInfo.checkedMutes}/{actions.tempMutes.Count}) in guild {sockGuild} owned by {sockGuild.Owner}");

                            if (editedMutes.Count != actions.tempMutes.Count)
                            {
                                if (debug) Console.Write($"{actions.tempMutes.Count - editedMutes.Count}/{actions.tempMutes.Count} tempmutes are over");
                                actions.tempMutes = editedMutes;
                                needSave = true;
                            }
                            else if (debug) Console.Write($"none of {actions.tempMutes.Count} tempmutes over");
                        }
                        else if (debug) Console.Write("no tempmutes to check or no settings");
                        if (needSave) actions.SaveToFile();
                    }
                    else if (debug) Console.Write("no actions to check");
                }
                if (debug) Console.Write("\n");
                _ = (currentInfo.checkedGuilds > 0).AssertWarnAsync("Checked 0 guilds for tempacts?");

            }
            catch (Exception e)
            {
                await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong checking temp actions", e).Log();
            }
        }

        public async Task ActCheckExec()
        {
            if (currentInfo.checking)
            {
                await new LogMessage(LogSeverity.Critical, "TempAct", $"Temp actions took longer than 30 seconds to complete and still haven't canceled\nIt has gone through {currentInfo?.checkedGuilds}/{client.Guilds.Count} guilds").Log();
                return;
            }

            currentInfo.checking = true;
            DateTime start = DateTime.UtcNow;
            await CheckTempActs(client);
            TimeSpan execTime = DateTime.UtcNow.Subtract(start);
            cachedInfo.checkExecutionTimes.Enqueue(execTime);
            cachedInfo.lastCheck = DateTime.UtcNow;
            currentInfo.checking = false;
        }
    }

    public class CachedTempActionInfo
    {
        public FixedSizedQueue<TimeSpan> checkExecutionTimes = new FixedSizedQueue<TimeSpan>(8);
        public DateTime lastCheck;
    }

    public class CurrentTempActionInfo
    {
        public bool checking = false;
        public int checkedGuilds = 0;
        public uint checkedMutes = 0;

        public List<TempAct> editedBans = null;
    }
}
