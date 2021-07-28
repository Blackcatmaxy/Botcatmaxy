using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace BotCatMaxy.Services.TempActions
{
    public class TempActionChecker
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger _log;
        private readonly bool _debug;
        private CancellationToken? _ct;
        private bool _finished = false;

        public TempActionChecker(DiscordSocketClient client, ILogger logger, bool debug = false)
        {
            _client = client;
            _debug = debug;
        }

        public void CheckCompletion()
        {
            if (_finished)
                return;
            _log.Log(LogEventLevel.Warning, "TempAct",
                $"Check taking longer than normal to complete and still haven't canceled.\nIt has gone through {CurrentInfo.CheckedGuilds}/{_client.Guilds.Count} guilds. Not starting new check.");
            throw new InvalidOperationException();
        }

        public async Task ExecuteAsync(CancellationToken? ct = null)
        {
            _ct = ct;
            try
            {
                await CheckTempActs();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                await new LogMessage(LogSeverity.Critical, "TempAct", "Something went wrong checking temp actions", e).Log();
            }

            _finished = true;
        }

        public async Task CheckTempActs()
        {
            RequestOptions requestOptions = RequestOptions.Default;
            requestOptions.RetryMode = RetryMode.AlwaysRetry;
            
            CurrentInfo.CheckedGuilds = 0;
            foreach (SocketGuild sockGuild in _client.Guilds)
            {
                _ct?.ThrowIfCancellationRequested();
                CurrentInfo.CheckedMutes = 0;
                if (CurrentInfo.CheckedGuilds > _client.Guilds.Count)
                {
                    await new LogMessage(LogSeverity.Error, "TempAct", $"Check went past all guilds (at #{CurrentInfo.CheckedGuilds}) but has been stopped. This doesn't seem physically possible.").Log();
                    return;
                }
                RestGuild restGuild = await _client.Rest.SuperGetRestGuild(sockGuild.Id);
                if (_debug)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"\nChecking {sockGuild.Name} discord ");
                }
                TempActionList actions = sockGuild.LoadFromFile<TempActionList>(false);
                bool needSave = false;
                CurrentInfo.CheckedGuilds++;
                if (actions != null)
                {
                    if (actions.tempBans?.Count is not null or 0)
                    {
                        CurrentInfo.EditedBans = new List<TempAct>(actions.tempBans);
                        foreach (TempAct tempBan in actions.tempBans)
                        {
                            try
                            {
                                RestBan ban = await sockGuild.GetBanAsync(tempBan.User, requestOptions);
                                if (ban == null)
                                { //If manual unban
                                    var user = await _client.Rest.GetUserAsync(tempBan.User);
                                    CurrentInfo.EditedBans.Remove(tempBan);
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
                                    await sockGuild.RemoveBanAsync(tempBan.User, requestOptions);
                                    CurrentInfo.EditedBans.Remove(tempBan);
                                    DiscordLogging.LogEndTempAct(sockGuild, rUser, "bann", tempBan.Reason, tempBan.Length);
                                }
                            }
                            catch (Exception e)
                            {
                                await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong unbanning someone, continuing", e).Log();
                            }
                        }

                        //if all tempbans DON'T equal all edited tempbans (basically if there was a change
                        if (CurrentInfo.EditedBans.Count != actions.tempBans.Count)
                        {
                            if (_debug) Console.Write($"{actions.tempBans.Count - CurrentInfo.EditedBans.Count} tempbans are over, ");
                            needSave = true;
                            actions.tempBans = CurrentInfo.EditedBans;
                        }
                        else if (_debug) Console.Write($"tempbans checked, none over, ");
                    }
                    else if (_debug) Console.Write($"no tempbans, ");
                    ModerationSettings settings = sockGuild.LoadFromFile<ModerationSettings>();
                    if (settings is not null && sockGuild.GetRole(settings.mutedRole) != null && actions.tempMutes?.Count is not null or 0)
                    {
                        var mutedRole = sockGuild.GetRole(settings.mutedRole);
                        List<TempAct> editedMutes = new List<TempAct>(actions.tempMutes);
                        foreach (TempAct tempMute in actions.tempMutes)
                        {
                            CurrentInfo.CheckedMutes++;
                            try
                            {
                                IGuildUser gUser = sockGuild.GetUser(tempMute.User) ?? await restGuild.SuperGetUser(tempMute.User);
                                if (gUser != null && !gUser.RoleIds.Contains(settings.mutedRole))
                                { //User missing muted role, must have been manually unmuted
                                    _ = gUser.TryNotify($"As you might know, you have been manually unmuted in {sockGuild.Name} discord");
                                    editedMutes.Remove(tempMute);
                                    DiscordLogging.LogManualEndTempAct(sockGuild, gUser, "mut", tempMute.DateBanned);
                                    (!editedMutes.Contains(tempMute)).Assert("Tempmute not removed?!");
                                }
                                else if (DateTime.UtcNow >= tempMute.DateBanned.Add(tempMute.Length))
                                { //Normal mute end
                                    if (gUser != null)
                                    {
                                        await gUser.RemoveRoleAsync(mutedRole, requestOptions);
                                    } // if user not in guild || if user doesn't contain muted role (successfully removed?
                                    if (gUser == null || !gUser.RoleIds.Contains(settings.mutedRole))
                                    { //Doesn't remove tempmute if unmuting fails
                                        IUser user = gUser; //Gets user to try to message
                                        user ??= await _client.SuperGetUser(tempMute.User);
                                        if (user != null)
                                        { // if possible to message, message and log
                                            DiscordLogging.LogEndTempAct(sockGuild, user, "mut", tempMute.Reason, tempMute.Length);
                                            _ = user.Notify("auto untempmuted", tempMute.Reason, sockGuild, _client.CurrentUser);
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
                        (CurrentInfo.CheckedMutes == actions.tempMutes.Count).Assert($"Checked incorrect number tempmutes ({CurrentInfo.CheckedMutes}/{actions.tempMutes.Count}) in guild {sockGuild} owned by {sockGuild.Owner}");

                        if (editedMutes.Count != actions.tempMutes.Count)
                        {
                            if (_debug) Console.Write($"{actions.tempMutes.Count - editedMutes.Count}/{actions.tempMutes.Count} tempmutes are over");
                            actions.tempMutes = editedMutes;
                            needSave = true;
                        }
                        else if (_debug) Console.Write($"none of {actions.tempMutes.Count} tempmutes over");
                    }
                    else if (_debug) Console.Write("no tempmutes to check or no settings");
                    if (needSave) actions.SaveToFile();
                }
                else if (_debug) Console.Write("no actions to check");
            }
            if (_debug) Console.Write("\n");
            (CurrentInfo.CheckedGuilds > 0).AssertWarn("Checked 0 guilds for tempacts?");
        }
    }
}