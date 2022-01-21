using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Services.Logging;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Serilog;
using Serilog.Context;
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
        private LogSettings _logSettings;
        private readonly ILogger _log;
        private ushort _checkedMutes;
        private ushort _checkedBans;
        private bool _needSave;

        public GuildActChecker(DiscordSocketClient client, SocketGuild guild, ILogger logger, CancellationToken ct,
            int count)
        {
            CurrentInfo.CheckedGuilds++;
            _guildIndex = $"{CurrentInfo.CheckedGuilds.ToString()}/{count.ToString()}";
            _client = client;
            _guild = guild;
            _log = logger.ForContext("GuildId", _guild.Id)
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
            LogContext.PushProperty("UserId", action.UserId);
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
                        throw new InvalidOperationException($"User:{action.UserId} still {actType}!");
                    await RemoveActionAsync(action, editedActions, ResolutionType.Normal);
                }
            }
            catch (Exception e)
            {
                _log.Error(e, "Something went wrong resolving {ActType} of {User}, continuing",
                    actType, action.UserId);
            }
        }

        private async Task RemoveActionAsync<TAction>(TAction action, List<TAction> editedActions,
            ResolutionType resolutionType) where TAction : TempAction
        {
            _needSave = true;
            editedActions.Remove(action);
            try
            {
                await LogUserActEnd(action, resolutionType);
            }
            catch (Exception e)
            {
                _log.Error(e, "Something went wrong logging end of TempAct");
            }
        }

        public async Task LogUserActEnd(TempAction action, ResolutionType resolutionType)
        {
            bool isManual = resolutionType == ResolutionType.Early;
            string type = $"{(isManual ? "manually" : "auto")} untemp{action.LogString}ed";

            var user = await _client.GetUserAsync(action.UserId);
            user?.Notify(type, action.Reason, _guild, _client.CurrentUser);
            _logSettings ??= _guild.LoadFromFile<LogSettings>();
            if (_guild.GetChannel(_logSettings.BestLog ?? 0) is not SocketTextChannel channel)
                return;

            var embed = new EmbedBuilder()
                        .AddField($"{user} has been {type}",
                            $"After {action.Length.LimitedHumanize(2)}, because of {action.Reason}")
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp();

            if (user != null)
                embed.WithAuthor(user);
            else
                embed.WithAuthor(new UserRef(action.UserId).ToString());
            await channel.SendMessageAsync(embed: embed.Build());
        }
    }
}