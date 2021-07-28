using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.Rest;
using Discord.WebSocket;
using Humanizer;
using Microsoft.Extensions.Logging;
using Polly;
using Serilog.Core;
using Serilog.Events;

namespace BotCatMaxy.Services.TempActions
{
    public class TempActionService : DiscordClientService
    {
        public TempActionChecker ActiveChecker { get; private set; }
        private readonly System.Timers.Timer _timer;
        private readonly DiscordSocketClient _client;
        private CancellationToken _shutdownToken;
        private ILogger<DiscordClientService> _botLogger;
        private Serilog.ILogger _verboseLogger;

        public TempActionService(DiscordSocketClient client, ILogger<DiscordClientService> logger) : base(client, logger)
        {
            _client = client;
            _timer = new System.Timers.Timer(45000);
            client.UserJoined += CheckNewUserAsync;
        }

        protected override async Task ExecuteAsync(CancellationToken shutdownToken)
        {
            _shutdownToken = shutdownToken;
            
            await _client.WaitForReadyAsync(shutdownToken);
            _timer.Elapsed += async (_, _) => await ActCheckExecAsync();
            _timer.Start();
        }

        /// <summary>
        /// Check user for mute and missing mute role. If satisfied add muted role.
        /// </summary>
        public static async Task CheckNewUserAsync(IGuildUser user)
        {
            var settings = user.Guild?.LoadFromFile<ModerationSettings>();
            var actions = user.Guild?.LoadFromFile<TempActionList>();
            
            //Can be done better and cleaner
            if (settings == null || user.Guild?.GetRole(settings.mutedRole) == null || (actions?.tempMutes?.Count is null or 0))
                return;
            if (actions.tempMutes.Any(tempMute => tempMute.User == user.Id))
                await user.AddRoleAsync(user.Guild.GetRole(settings.mutedRole));
        }

        /// <summary>
        /// Initialize the act check and perform sanity checks
        /// </summary>
        public Task ActCheckExecAsync()
        {
            ActiveChecker?.CheckCompletion();
            ActiveChecker = new TempActionChecker(_client, _verboseLogger);
            
            CurrentInfo.Checking = true;
            var start = DateTime.UtcNow;
            var timeoutPolicy = Policy.TimeoutAsync(40, Polly.Timeout.TimeoutStrategy.Optimistic, (context, timespan, task) =>
            {
                _verboseLogger.Write(LogEventLevel.Error, "TempAct",
                    $"TempAct check canceled at {DateTime.UtcNow.Subtract(start).Humanize(2)} and through {CurrentInfo.CheckedGuilds}/{_client.Guilds.Count} guilds");
                //Won't continue to below so have to do this?
                ResetInfo(start);
                return Task.CompletedTask;
            });
            
            return timeoutPolicy.ExecuteAsync(async ct =>
            {
                await ActiveChecker.ExecuteAsync(ct);
                ResetInfo(start);
            }, _shutdownToken, false);
        }

        public void ResetInfo(DateTime start)
        {
            TimeSpan execTime = DateTime.UtcNow.Subtract(start);
            CachedInfo.CheckExecutionTimes.Enqueue(execTime);
            CachedInfo.LastCheck = DateTime.UtcNow;
            CurrentInfo.Checking = false;
        }
    }
}
