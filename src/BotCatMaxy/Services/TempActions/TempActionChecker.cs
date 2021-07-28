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
        private CancellationToken? _ct;
        private bool _finished = false;

        public TempActionChecker(DiscordSocketClient client, ILogger logger)
        {
            _client = client;
            _log = logger;
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
                _log.Verbose("Check starting");
                await CheckTempActs();
                _log.Verbose("Check completed");
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                await LogSeverity.Critical.LogExceptionAsync("TempAct", "Something went wrong checking TempActions", e);
                _log.Information("Something went wrong checking TempActions");
            }
            finally
            {
                _finished = true;
            }
        }

        public async Task CheckTempActs()
        {
            RequestOptions requestOptions = RequestOptions.Default;
            requestOptions.RetryMode = RetryMode.AlwaysRetry;
            
            CurrentInfo.CheckedGuilds = 0;
            var guildCount = (byte)_client.Guilds.Count;
            foreach (SocketGuild sockGuild in _client.Guilds)
            {
                _ct?.ThrowIfCancellationRequested();
                CurrentInfo.CheckedMutes = 0;
                if (CurrentInfo.CheckedGuilds > _client.Guilds.Count)
                {
                    _log.Log(LogEventLevel.Error, "TempAct",
                        $"Check went past all guilds (at #{CurrentInfo.CheckedGuilds.ToString()}) but has been stopped. This doesn't seem physically possible.");
                    return;
                }
                RestGuild restGuild = await _client.Rest.SuperGetRestGuild(sockGuild.Id);
                _log.Verbose($"Checking {sockGuild.Name} guild ({CurrentInfo.CheckedGuilds.ToString()}/{guildCount.ToString()}).");
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
                                await LogSeverity.Error.LogExceptionAsync("TempAct",
                                    "Something went wrong unbanning someone, continuing.", e);
                                _log.Information("Something went wrong unbanning someone, continuing.", e);
                            }
                        }

                        //If there was a change in the number of TempBans
                        if (CurrentInfo.EditedBans.Count != actions.tempBans.Count)
                        {
                            _log.Verbose($"{(actions.tempBans.Count - CurrentInfo.EditedBans.Count).ToString()} TempBans are over.");
                            needSave = true;
                            actions.tempBans = CurrentInfo.EditedBans;
                        }
                        else _log.Verbose($"None of {actions.tempBans.Count.ToString()} TempBans over");
                    }
                    else _log.Verbose("No TempBans in guild.");
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
                                    else if (gUser != null)
                                        _log.Log(LogEventLevel.Warning, "TempAct", "User should've had role removed");
                                }
                            }
                            catch (Exception e)
                            {
                                await LogSeverity.Error.LogExceptionAsync("TempAct", "Something went wrong unmuting someone, continuing", e);
                            }
                        }

                        //NOTE: Assertions fail if NOT true
                        (CurrentInfo.CheckedMutes == actions.tempMutes.Count).Assert($"Checked incorrect number of TempMutes ({CurrentInfo.CheckedMutes}/{actions.tempMutes.Count}) in guild {sockGuild} owned by {sockGuild.Owner}");

                        if (editedMutes.Count != actions.tempMutes.Count)
                        {
                            _log.Verbose($"{(actions.tempMutes.Count - editedMutes.Count).ToString()}/{actions.tempMutes.Count.ToString()} TempMutes are over.");
                            actions.tempMutes = editedMutes;
                            needSave = true;
                        }
                        else _log.Verbose($"None of {actions.tempMutes.Count.ToString()} TempMutes over");
                    }
                    else _log.Verbose("No TempMutes to check or no settings");
                    if (needSave) actions.SaveToFile();
                }
                else _log.Verbose("No actions to check.");
            }
            (CurrentInfo.CheckedGuilds > 0).AssertWarn("Checked 0 guilds for TempActs?");
        }
    }
}