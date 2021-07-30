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
        private CancellationToken _ct;
        private bool _finished;

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

        public async Task ExecuteAsync(CancellationToken ct)
        {
            _ct = ct;
            try
            {
                _log.Verbose("Check starting");
                await CheckTempActsAsync();
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

        private Task CheckTempActsAsync()
        {
            CurrentInfo.CheckedGuilds = 0;
            var guildCount = (byte)_client.Guilds.Count;
            var tasks = new Task[guildCount];
            var i = 0;
            foreach (var guild in _client.Guilds)
            {
                tasks[i] = CheckGuildAsync(guild, guildCount);
                i++;
            }

            return Task.WhenAll(tasks);
        }

        private Task CheckGuildAsync(SocketGuild guild, int guildCount)
        {
            _ct.ThrowIfCancellationRequested();
            var guildCheck = new GuildActChecker(_client, guild, _log, _ct, guildCount);

            return guildCheck.CheckGuildAsync();
        }
    }
}