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
        private readonly RequestOptions _requestOptions = new();
        private readonly DiscordSocketClient _client;
        private readonly CancellationToken? _ct;
        private readonly SocketGuild _guild;
        private readonly string _guildIndex;
        private readonly ILogger _log;
        private ushort _checkedMutes;
        private ushort _checkedBans;
        private bool _needSave;

        public GuildActChecker(DiscordSocketClient client, SocketGuild guild, ILogger logger, CancellationToken ct, int count)
        {
            CurrentInfo.CheckedGuilds++;
            _guildIndex = $"{CurrentInfo.CheckedGuilds.ToString()}/{count.ToString()}";
            _client = client;
            _guild = guild;
            _log = logger.ForContext("GuildID", _guild.Id.ToString())
                         .ForContext("GuildIndex", _guildIndex);
            _ct = ct;
            _requestOptions.CancelToken = ct;
            _requestOptions.RetryMode = RetryMode.AlwaysRetry;
        }

        public async Task CheckGuildAsync()
        {
            _ct?.ThrowIfCancellationRequested();
            _log.Verbose("Checking {Name} guild ({GuildIndex})", _guild.Name, _guildIndex);
            var actions = _guild.LoadFromFile<TempActionList>();
            if (actions != null)
            {
                var banTask = CheckTempActsAsync(actions.tempBans);
                var muteTask = CheckTempActsAsync(actions.tempMutes);
                await Task.WhenAll(banTask, muteTask);
                if (_needSave)
                {
                    actions.tempBans = banTask.Result;
                    actions.tempMutes = muteTask.Result;
                    actions.SaveToFile();
                }
            }
            else
            {
                _log.Verbose("No actions to check");
            }
        }

        public async Task<List<TAction>> CheckTempActsAsync<TAction>(List<TAction> actions) where TAction : TempAction
        {
            string actType = typeof(TAction).Name;
            if (actions?.Count is null or 0)
            {
                _log.Verbose("No {ActType}s in guild", actType);
                return actions;
            }

            var editedActions = new List<TAction>(actions);
            foreach (var action in actions)
            {
                _ct?.ThrowIfCancellationRequested();
                await CheckTempAct(action, editedActions);
            }

            int delta = editedActions.Count - actions.Count;
            _log.Information("Checked {Actions} {ActType}s, ended up with {EditedActions} (delta {Delta})",
                actions.Count, actType, editedActions.Count, delta);
            return editedActions;
        }

        public async Task CheckTempAct<TAction>(TAction action, List<TAction> editedActions) where TAction : TempAction
        {
            string actType = typeof(TAction).Name;
            try
            {
                if (await action.CheckResolvedAsync(_guild, ResolutionType.Early, _requestOptions))
                {
                    await RemoveActionAsync(action, editedActions, ResolutionType.Early);
                }
                else if (action.ShouldEnd)
                {
                    await action.ResolveAsync(_guild, _requestOptions);
                    var result = await action.CheckResolvedAsync(_guild, ResolutionType.Normal, _requestOptions);
                    if (result == false)
                        throw new Exception($"User:{action.UserId} still {actType}!");
                    await RemoveActionAsync(action, editedActions, ResolutionType.Normal);
                }
            }
            catch (Exception e)
            {
                await LogSeverity.Error.SendExceptionAsync("TempAct", $"Something went wrong resolving {actType}, continuing.", e);
                _log.Information(e, "Something went wrong resolving {ActType} of {User}, continuing",
                    actType, action.UserId);
            }
        }

        private async Task RemoveActionAsync<TAction>(TAction action, List<TAction> editedActions, ResolutionType resolutionType) where TAction : TempAction
        {
            _needSave = true;
            editedActions.Remove(action);
            await action.LogEndAsync(_guild, _client, resolutionType);
        }

        // private async Task CheckTempBansAsync(TempActionList actions)
        // {
        //     if (actions.tempBans?.Count is null or 0)
        //     {
        //         _log.Verbose("No TempBans in guild");
        //         return;
        //     }
        //
        //     var editedBans = new List<TempBan>(actions.tempBans);
        //     foreach (var tempBan in actions.tempBans)
        //     {
        //         await CheckTempBanAsync(tempBan, editedBans);
        //     }
        //
        //     //If there was a change in the number of TempBans
        //     if (_checkedBans != actions.tempBans.Count)
        //     {
        //         _log.Verbose("{Count} TempBans are over", actions.tempBans.Count - editedBans.Count);
        //         _needSave = true;
        //         actions.tempBans = editedBans;
        //     }
        //     else
        //     {
        //         _log.Verbose("None of {Count} TempBans over", actions.tempBans.Count);
        //     }
        // }
        //
        // public async Task CheckTempBanAsync(TempBan tempBan, List<TempBan> editedBans)
        // {
        //     _ct?.ThrowIfCancellationRequested();
        //     _checkedBans++;
        //     try
        //     {
        //         var ban = await _guild.GetBanAsync(tempBan.UserId, _requestOptions);
        //         if (ban == null)
        //         {
        //             // If user has been manually unbanned by another user (missing ban)
        //             var user = await _client.Rest.GetUserAsync(tempBan.UserId);
        //             editedBans.Remove(tempBan);
        //             user?.TryNotify($"As you might know, you have been manually unbanned in {_guild.Name} discord");
        //             var userRef = (user != null) ? new UserRef(user) : new UserRef(tempBan.UserId);
        //             await _guild.LogEndTempAct(userRef, "bann", tempBan.Reason, tempBan.Length, true);
        //         }
        //         else if (DateTime.UtcNow >= tempBan.Start.Add(tempBan.Length))
        //         {
        //             // If user's TempBan has ended normally
        //             var user = ban.User;
        //             await _guild.RemoveBanAsync(tempBan.User, _requestOptions);
        //             editedBans.Remove(tempBan);
        //             await _guild.LogEndTempAct(new UserRef(user), "bann", tempBan.Reason, tempBan.Length);
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         await LogSeverity.Error.LogExceptionAsync("TempAct",
        //             "Something went wrong unbanning someone, continuing.", e);
        //         _log.Information(e, "Something went wrong unbanning someone, continuing");
        //     }
        // }
        //
        // public async Task CheckTempMutesAsync(TempActionList actions)
        // {
        //     var settings = _guild.LoadFromFile<ModerationSettings>();
        //     if (settings is null || _guild.GetRole(settings.mutedRole) == null || actions.tempMutes?.Count is null or 0)
        //     {
        //         _log.Verbose("No TempMutes to check or no settings");
        //         return;
        //     }
        //
        //     var restGuild = await _client.Rest.SuperGetRestGuild(_guild.Id);
        //     var editedMutes = new List<TempAct>(actions.tempMutes);
        //     foreach (var tempMute in actions.tempMutes)
        //     {
        //         await CheckTempMuteAsync(tempMute, editedMutes, restGuild, settings);
        //     }
        //
        //     //NOTE: Assertions fail if NOT true
        //     (_checkedMutes == actions.tempMutes.Count).Assert(
        //         $"Checked incorrect number of TempMutes ({_checkedMutes.ToString()}/{actions.tempMutes.Count.ToString()}) in guild {_guild} owned by {_guild.Owner}.");
        //
        //     if (editedMutes.Count != actions.tempMutes.Count)
        //     {
        //         var ended = (ushort)(actions.tempMutes.Count - editedMutes.Count);
        //         _log.Verbose("{Ended}/{Count} TempMutes are over", ended, actions.tempMutes.Count);
        //         actions.tempMutes = editedMutes;
        //         _needSave = true;
        //     }
        //     else
        //     {
        //         _log.Verbose("None of {Count} TempMutes are over", actions.tempMutes.Count);
        //     }
        // }

        // public async Task CheckTempMuteAsync(TempAct tempMute, List<TempAct> editedMutes, RestGuild restGuild, ModerationSettings settings)
        // {
        //     _ct?.ThrowIfCancellationRequested();
        //     _checkedMutes++;
        //     try
        //     {
        //         var gUser = _guild.GetUser(tempMute.User) ?? await restGuild.SuperGetUser(tempMute.User);
        //         if (gUser != null && !gUser.RoleIds.Contains(settings.mutedRole))
        //         {
        //             //If user has been manually unmuted by another user (missing role)
        //             _ = gUser.TryNotify($"As you might know, you have manually been unmuted in {_guild.Name}");
        //             editedMutes.Remove(tempMute);
        //             await _guild.LogEndTempAct(new UserRef(gUser), "mut", tempMute.Reason, tempMute.Length, true);
        //             (!editedMutes.Contains(tempMute)).Assert("TempMute not removed?!");
        //         }
        //         else if (DateTime.UtcNow >= tempMute.DateBanned.Add(tempMute.Length))
        //         {
        //             // If user's TempBan has ended normally
        //
        //             if (gUser != null)
        //                 await gUser.RemoveRoleAsync(settings.mutedRole, _requestOptions);
        //
        //             // Gets user object again just to be sure mute was removed
        //             if (gUser != null) gUser = _guild.GetUser(tempMute.User) ?? await restGuild.SuperGetUser(tempMute.User);
        //
        //             // If user not in guild, OR if user properly had role removed
        //             if (gUser == null || !gUser.RoleIds.Contains(settings.mutedRole))
        //             {
        //                 UserRef userRef;
        //                 if ((gUser ?? await _client.SuperGetUser(tempMute.User)) is not null and var user)
        //                 {
        //                     // if possible to message, message and log
        //                     userRef = new UserRef(user);
        //                     _ = user.Notify("auto untempmuted", tempMute.Reason, _guild, _client.CurrentUser);
        //                 }
        //                 else
        //                 {
        //                     userRef = new UserRef(tempMute.User);
        //                 }
        //                 await _guild.LogEndTempAct(userRef, "mut", tempMute.Reason, tempMute.Length);
        //                 editedMutes.Remove(tempMute);
        //             }
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         await LogSeverity.Error.LogExceptionAsync("TempAct", "Something went wrong unmuting someone, continuing.", e);
        //         _log.Information(e, "Something went wrong unmuting someone, continuing");
        //     }
        // }
    }
}