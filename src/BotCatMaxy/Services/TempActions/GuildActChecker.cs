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
using Serilog.Events;
using Serilog.Parsing;

namespace BotCatMaxy.Services.TempActions
{
    public class GuildActChecker
    {
        private readonly List<WrittenLogEvent> _logEvents = new();
        private readonly RequestOptions _requestOptions = new();
        private readonly DiscordSocketClient _client;
        private readonly CancellationToken? _ct;
        private readonly SocketGuild _guild;
        private readonly int _guildCount;
        private readonly ILogger _log;
        private ushort _checkedMutes;
        private ushort _checkedBans;
        private bool _finished = false;
        private bool _needSave = false;

        public GuildActChecker(DiscordSocketClient client, SocketGuild guild, ILogger logger, CancellationToken ct, int count)
        {
            _client = client;
            _guild = guild;
            _log = logger;
            _ct = ct;
            _guildCount = count;
            _requestOptions.RetryMode = RetryMode.AlwaysRetry;
        }

        private void LogToList(LogEventLevel level, string content, Exception exception = null)
            => _logEvents.Add(new WrittenLogEvent(level, content, exception));

        public async Task CheckGuildAsync()
        {
            _ct?.ThrowIfCancellationRequested();
            LogToList(LogEventLevel.Verbose, $"Checking {_guild.Name} guild ({CurrentInfo.CheckedGuilds.ToString()}/{_guildCount.ToString()}).");
            CurrentInfo.CheckedGuilds++;
            var actions = _guild.LoadFromFile<TempActionList>();
            if (actions != null)
            {
                var banTask = CheckTempBansAsync(actions);
                var muteTask = CheckTempMutesAsync(actions);
                await Task.WhenAll(banTask, muteTask);
                if (_needSave)
                    actions.SaveToFile();
            }
            else
            {
                LogToList(LogEventLevel.Verbose, "No actions to check.");
            }

            foreach (var logEvent in _logEvents)
            {
                _log.Write(logEvent.EventLevel, logEvent.Content, logEvent.TimeOffset, logEvent.Exception);
            }
        }

        private async Task CheckTempBansAsync(TempActionList actions)
        {
            if (actions.tempBans?.Count is not null or 0)
            {
                var editedBans = new List<TempAct>(actions.tempBans);
                foreach (var tempBan in actions.tempBans)
                {
                    _ct?.ThrowIfCancellationRequested();
                    _checkedBans++;
                    try
                    {
                        RestBan ban = await _guild.GetBanAsync(tempBan.User, _requestOptions);
                        if (ban == null)
                        { //If manual unban
                            var user = await _client.Rest.GetUserAsync(tempBan.User);
                            editedBans.Remove(tempBan);
                            user?.TryNotify($"As you might know, you have been manually unbanned in {_guild.Name} discord");
                            //_ = new LogMessage(LogSeverity.Warning, "TempAction", "Tempbanned person isn't banned").Log();
                            if (user == null)
                                DiscordLogging.LogManualEndTempAct(_guild, tempBan.User, "bann", tempBan.DateBanned);
                            else
                                DiscordLogging.LogManualEndTempAct(_guild, user, "bann", tempBan.DateBanned);
                        }
                        else if (DateTime.UtcNow >= tempBan.DateBanned.Add(tempBan.Length))
                        {
                            RestUser rUser = ban.User;
                            await _guild.RemoveBanAsync(tempBan.User, _requestOptions);
                            editedBans.Remove(tempBan);
                            DiscordLogging.LogEndTempAct(_guild, rUser, "bann", tempBan.Reason, tempBan.Length);
                        }
                    }
                    catch (Exception e)
                    {
                        await LogSeverity.Error.LogExceptionAsync("TempAct",
                            "Something went wrong unbanning someone, continuing.", e);
                        LogToList(LogEventLevel.Information, "Something went wrong unbanning someone, continuing.", e);
                    }
                }

                //If there was a change in the number of TempBans
                if (_checkedBans != actions.tempBans.Count)
                {
                    LogToList(LogEventLevel.Verbose, $"{(actions.tempBans.Count - editedBans.Count).ToString()} TempBans are over.");
                    _needSave = true;
                    actions.tempBans = editedBans;
                }
                else LogToList(LogEventLevel.Verbose, $"None of {actions.tempBans.Count.ToString()} TempBans over.");
            }
            else LogToList(LogEventLevel.Verbose, "No TempBans in guild.");
        }

        public async Task CheckTempMutesAsync(TempActionList actions)
        {
            var settings = _guild.LoadFromFile<ModerationSettings>();
            if (settings is not null && _guild.GetRole(settings.mutedRole) != null && actions.tempMutes?.Count is not null or 0)
            {
                RestGuild restGuild = await _client.Rest.SuperGetRestGuild(_guild.Id);
                var mutedRole = _guild.GetRole(settings.mutedRole);
                var editedMutes = new List<TempAct>(actions.tempMutes);
                foreach (var tempMute in actions.tempMutes)
                {
                    _ct?.ThrowIfCancellationRequested();
                    _checkedMutes++;
                    try
                    {
                        IGuildUser gUser = _guild.GetUser(tempMute.User) ?? await restGuild.SuperGetUser(tempMute.User);
                        if (gUser != null && !gUser.RoleIds.Contains(settings.mutedRole))
                        { //User missing muted role, must have been manually unmuted
                            _ = gUser.TryNotify($"As you might know, you have been manually unmuted in {_guild.Name} discord");
                            editedMutes.Remove(tempMute);
                            DiscordLogging.LogManualEndTempAct(_guild, gUser, "mut", tempMute.DateBanned);
                            (!editedMutes.Contains(tempMute)).Assert("Tempmute not removed?!");
                        }
                        else if (DateTime.UtcNow >= tempMute.DateBanned.Add(tempMute.Length))
                        { //Normal mute end
                            if (gUser != null)
                            {
                                await gUser.RemoveRoleAsync(mutedRole, _requestOptions);
                            } // if user not in guild || if user doesn't contain muted role (successfully removed?
                            if (gUser == null || !gUser.RoleIds.Contains(settings.mutedRole))
                            { //Doesn't remove tempmute if unmuting fails
                                IUser user = gUser; //Gets user to try to message
                                user ??= await _client.SuperGetUser(tempMute.User);
                                if (user != null)
                                { // if possible to message, message and log
                                    DiscordLogging.LogEndTempAct(_guild, user, "mut", tempMute.Reason, tempMute.Length);
                                    _ = user.Notify("auto untempmuted", tempMute.Reason, _guild, _client.CurrentUser);
                                }
                                editedMutes.Remove(tempMute);
                            }
                            else if (gUser != null)
                                _log.Log(LogEventLevel.Warning, "TempAct", "User should've had role removed.");
                        }
                    }
                    catch (Exception e)
                    {
                        await LogSeverity.Error.LogExceptionAsync("TempAct", "Something went wrong unmuting someone, continuing.", e);
                    }
                }

                //NOTE: Assertions fail if NOT true
                (_checkedMutes == actions.tempMutes.Count).Assert(
                    $"Checked incorrect number of TempMutes ({_checkedMutes}/{actions.tempMutes.Count}) in guild {_guild} owned by {_guild.Owner}.");

                if (editedMutes.Count != actions.tempMutes.Count)
                {
                    LogToList(LogEventLevel.Verbose, $"{(actions.tempMutes.Count - editedMutes.Count).ToString()}/{actions.tempMutes.Count.ToString()} TempMutes are over.");
                    actions.tempMutes = editedMutes;
                    _needSave = true;
                }
                else LogToList(LogEventLevel.Verbose, $"None of {actions.tempMutes.Count.ToString()} TempMutes over.");
            }
            else LogToList(LogEventLevel.Verbose, "No TempMutes to check or no settings.");
        }
    }
}