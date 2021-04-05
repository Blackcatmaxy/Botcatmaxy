using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Moderation;
using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Humanizer;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    public class TempActions
    {
        readonly DiscordSocketClient client;
        public static CurrentTempActionInfo currentInfo = new CurrentTempActionInfo();
        public static CachedTempActionInfo cachedInfo = new CachedTempActionInfo();
        private static readonly int[] ignoredHTTPErrors = { 500, 503, 530 };
        private static AsyncRetryPolicy retryPolicy;
        private Timer timer;

        public TempActions(DiscordSocketClient client)
        {
            this.client = client;
            client.Ready += Ready;
            client.UserJoined += CheckNewUser;
            retryPolicy = Policy
                .Handle<HttpException>(e => ignoredHTTPErrors.Contains((int)e.HttpCode))
                .RetryAsync(3);
        }

        private async Task Ready()
        {
            client.Ready -= Ready;
            timer = new Timer((_) => _ = ActCheckExec());
            timer.Change(0, 45000);
        }

        private async Task CheckNewUser(SocketGuildUser user)
        {
            ModerationSettings settings = user.Guild?.LoadFromFile<ModerationSettings>();
            TempActionList actions = user.Guild?.LoadFromFile<TempActionList>();
            //Can be done better and cleaner
            if (settings == null || user.Guild?.GetRole(settings.mutedRole) == null || (actions?.tempMutes?.Count is null or 0)) return;
            if (actions.tempMutes.Any(tempMute => tempMute.User == user.Id))
                await user.AddRoleAsync(user.Guild.GetRole(settings.mutedRole));
        }

        public static async Task CheckTempActs(DiscordSocketClient client, bool debug = false, CancellationToken? ct = null)
        {
            RequestOptions requestOptions = RequestOptions.Default;
            requestOptions.RetryMode = RetryMode.AlwaysRetry;
            try
            {
                currentInfo.checkedGuilds = 0;
                foreach (SocketGuild sockGuild in client.Guilds)
                {
                    ct?.ThrowIfCancellationRequested();
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
                        if (actions.tempBans?.Count is not null or 0)
                        {
                            currentInfo.editedBans = new List<TempAct>(actions.tempBans);
                            foreach (TempAct tempBan in actions.tempBans)
                            {
                                try
                                {
                                    var banResult = await retryPolicy.ExecuteAndCaptureAsync(async () => await sockGuild.GetBanAsync(tempBan.User, requestOptions));
                                    RestBan ban = banResult.FinalHandledResult;
                                    if (ban == null)
                                    { //If manual unban
                                        var user = await client.Rest.GetUserAsync(tempBan.User, requestOptions);
                                        currentInfo.editedBans.Remove(tempBan);
                                        user?.TryNotify($"As you might know, you have been manually unbanned in {sockGuild.Name} discord");
                                        //_ = new LogMessage(LogSeverity.Warning, "TempAction", "Tempbanned person isn't banned").Log();
                                        if (user == null)
                                            DiscordLogging.LogManualEndTempAct(sockGuild, tempBan.User, "bann", tempBan.DateBanned);
                                        else
                                            DiscordLogging.LogManualEndTempAct(sockGuild, user, "bann", tempBan.DateBanned);
                                    }
                                    else if (DateTime.UtcNow >= tempBan.DateBanned.Add(tempBan.Length))
                                    {
                                        RestUser rUser = ban.User;
                                        await retryPolicy.ExecuteAsync(async () => await sockGuild.RemoveBanAsync(tempBan.User, requestOptions));
                                        currentInfo.editedBans.Remove(tempBan);
                                        DiscordLogging.LogEndTempAct(sockGuild, rUser, "bann", tempBan.Reason, tempBan.Length);
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
                        if (settings is not null && sockGuild.GetRole(settings.mutedRole) != null && actions.tempMutes?.Count is not null or 0)
                        {
                            var mutedRole = sockGuild.GetRole(settings.mutedRole);
                            List<TempAct> editedMutes = new List<TempAct>(actions.tempMutes);
                            foreach (TempAct tempMute in actions.tempMutes)
                            {
                                currentInfo.checkedMutes++;
                                try
                                {
                                    IGuildUser gUser = sockGuild.GetUser(tempMute.User) ?? await restGuild.SuperGetUser(tempMute.User);
                                    if (gUser != null && !gUser.RoleIds.Contains(settings.mutedRole))
                                    { //User missing muted role, must have been manually unmuted
                                        _ = gUser.TryNotify($"As you might know, you have been manually unmuted in {sockGuild.Name} discord");
                                        editedMutes.Remove(tempMute);
                                        DiscordLogging.LogManualEndTempAct(sockGuild, gUser, "mut", tempMute.DateBanned);
                                        _ = (!editedMutes.Contains(tempMute)).AssertAsync("Tempmute not removed?!");
                                    }
                                    else if (DateTime.UtcNow >= tempMute.DateBanned.Add(tempMute.Length))
                                    { //Normal mute end
                                        if (gUser != null)
                                        {
                                            await retryPolicy.ExecuteAsync(async () => await gUser.RemoveRoleAsync(mutedRole, requestOptions));
                                        } // if user not in guild || if user doesn't contain muted role (successfully removed?
                                        if (gUser == null || !gUser.RoleIds.Contains(settings.mutedRole))
                                        { //Doesn't remove tempmute if unmuting fails
                                            IUser user = gUser; //Gets user to try to message
                                            user ??= await client.SuperGetUser(tempMute.User);
                                            if (user != null)
                                            { // if possible to message, message and log
                                                DiscordLogging.LogEndTempAct(sockGuild, user, "mut", tempMute.Reason, tempMute.Length);
                                                _ = user.Notify("auto untempmuted", tempMute.Reason, sockGuild, client.CurrentUser);
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
            catch (Exception e) when (e is not OperationCanceledException)
            {
                await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong checking temp actions", e).Log();
            }
        }

        public async Task ActCheckExec()
        {
            if (currentInfo.checking)
            {
                await new LogMessage(LogSeverity.Critical, "TempAct",
                    $"Check took longer than 30 seconds to complete and still haven't canceled\nIt has gone through {currentInfo?.checkedGuilds}/{client.Guilds.Count} guilds").Log();
                return;
            }

            currentInfo.checking = true;
            DateTime start = DateTime.UtcNow;
            var timeoutPolicy = Policy.TimeoutAsync(40, Polly.Timeout.TimeoutStrategy.Optimistic, onTimeoutAsync: async (context, timespan, task) =>
            {
                await new LogMessage(LogSeverity.Critical, "TempAct",
                    $"TempAct check canceled at {DateTime.UtcNow.Subtract(start).Humanize(precision: 2)} and through {currentInfo?.checkedGuilds}/{client.Guilds.Count} guilds").Log();
                //Won't continue to below so have to do this?
                ResetInfo(start);
            });
            await timeoutPolicy.ExecuteAsync(async ct => await CheckTempActs(client, ct: ct), CancellationToken.None, false);
            //Won't continue if above times out?
            ResetInfo(start);
        }

        public void ResetInfo(DateTime start)
        {
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
