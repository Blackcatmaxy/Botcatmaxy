using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Serilog;
using Serilog.Events;

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
        private bool _needSave;

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
                    await CheckTempBanAsync(tempBan, editedBans);
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

        public async Task CheckTempBanAsync(TempAct tempBan, List<TempAct> editedBans)
        {
            _ct?.ThrowIfCancellationRequested();
            _checkedBans++;
            try
            {
                var ban = await _guild.GetBanAsync(tempBan.User, _requestOptions);
                if (ban == null)
                {
                    // If user has been manually unbanned by another user (missing ban)
                    var user = await _client.Rest.GetUserAsync(tempBan.User);
                    editedBans.Remove(tempBan);
                    user?.TryNotify($"As you might know, you have been manually unbanned in {_guild.Name} discord");
                    var userRef = (user != null) ? new UserRef(user) : new UserRef(tempBan.User);
                    await _guild.LogEndTempAct(userRef, "bann", tempBan.Reason, tempBan.Length, true);
                }
                else if (DateTime.UtcNow >= tempBan.DateBanned.Add(tempBan.Length))
                {
                    // If user's TempBan has ended normally
                    var user = ban.User;
                    await _guild.RemoveBanAsync(tempBan.User, _requestOptions);
                    editedBans.Remove(tempBan);
                    await _guild.LogEndTempAct(new UserRef(user), "bann", tempBan.Reason, tempBan.Length);
                }
            }
            catch (Exception e)
            {
                await LogSeverity.Error.LogExceptionAsync("TempAct",
                    "Something went wrong unbanning someone, continuing.", e);
                LogToList(LogEventLevel.Information, "Something went wrong unbanning someone, continuing.", e);
            }
        }

        public async Task CheckTempMutesAsync(TempActionList actions)
        {
            var settings = _guild.LoadFromFile<ModerationSettings>();
            if (settings is not null && _guild.GetRole(settings.mutedRole) != null && actions.tempMutes?.Count is not null or 0)
            {
                var restGuild = await _client.Rest.SuperGetRestGuild(_guild.Id);
                var editedMutes = new List<TempAct>(actions.tempMutes);
                foreach (var tempMute in actions.tempMutes)
                {
                    await CheckTempMuteAsync(tempMute, editedMutes, restGuild, settings);
                }

                //NOTE: Assertions fail if NOT true
                (_checkedMutes == actions.tempMutes.Count).Assert(
                    $"Checked incorrect number of TempMutes ({_checkedMutes.ToString()}/{actions.tempMutes.Count.ToString()}) in guild {_guild} owned by {_guild.Owner}.");

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

        public async Task CheckTempMuteAsync(TempAct tempMute, List<TempAct> editedMutes, RestGuild restGuild, ModerationSettings settings)
        {
            _ct?.ThrowIfCancellationRequested();
            _checkedMutes++;
            try
            {
                var gUser = _guild.GetUser(tempMute.User) ?? await restGuild.SuperGetUser(tempMute.User);
                if (gUser != null && !gUser.RoleIds.Contains(settings.mutedRole))
                {
                    //If user has been manually unmuted by another user (missing role)
                    _ = gUser.TryNotify($"As you might know, you have manually been unmuted in {_guild.Name}");
                    editedMutes.Remove(tempMute);
                    await _guild.LogEndTempAct(new UserRef(gUser), "mut", tempMute.Reason, tempMute.Length, true);
                    (!editedMutes.Contains(tempMute)).Assert("TempMute not removed?!");
                }
                else if (DateTime.UtcNow >= tempMute.DateBanned.Add(tempMute.Length))
                {
                    // If user's TempBan has ended normally

                    if (gUser != null)
                        await gUser.RemoveRoleAsync(settings.mutedRole, _requestOptions);

                    // Gets user object again just to be sure mute was removed
                    if (gUser != null) gUser = _guild.GetUser(tempMute.User) ?? await restGuild.SuperGetUser(tempMute.User);

                    // If user not in guild, OR if user properly had role removed
                    if (gUser == null || !gUser.RoleIds.Contains(settings.mutedRole))
                    {
                        UserRef userRef;
                        if ((gUser ?? await _client.SuperGetUser(tempMute.User)) is not null and var user)
                        {
                            // if possible to message, message and log
                            userRef = new UserRef(user);
                            _ = user.Notify("auto untempmuted", tempMute.Reason, _guild, _client.CurrentUser);
                        }
                        else
                        {
                            userRef = new UserRef(tempMute.User);
                        }
                        await _guild.LogEndTempAct(userRef, "mut", tempMute.Reason, tempMute.Length);
                        editedMutes.Remove(tempMute);
                    }
                }
            }
            catch (Exception e)
            {
                await LogSeverity.Error.LogExceptionAsync("TempAct", "Something went wrong unmuting someone, continuing.", e);
            }
        }
    }
}