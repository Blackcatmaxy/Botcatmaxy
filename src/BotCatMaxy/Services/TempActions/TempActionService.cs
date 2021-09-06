using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.WebSocket;
using Humanizer;
using Microsoft.Extensions.Logging;
using Polly;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace BotCatMaxy.Services.TempActions
{
    public class TempActionService : DiscordClientService
    {
        public TempActionChecker ActiveChecker { get; private set; }
        private TempActionSink.FlushLogDelegate _flushLogDelegate;
        private readonly DiscordSocketClient _client;
        private readonly System.Timers.Timer _timer;
        private CancellationToken _shutdownToken;
        private Serilog.ILogger _verboseLogger;
        private DateTime _lastFlush;

        public TempActionService(DiscordSocketClient client, ILogger<DiscordClientService> logger) : base(client, logger)
        {
            _client = client;
            _timer = new System.Timers.Timer(45000);
            client.UserJoined += CheckNewUserAsync;
            _lastFlush = DateTime.Now;
        }

        protected override async Task ExecuteAsync(CancellationToken shutdownToken)
        {
            _shutdownToken = shutdownToken;
            await _client.WaitForReadyAsync(shutdownToken);
            _verboseLogger = new LoggerConfiguration()
                             .MinimumLevel.Verbose()
                             .WriteTo.Logger(Log.Logger, LogEventLevel.Warning)
                             .WriteTo.TempActionSink(_client, LogEventLevel.Verbose, out var flushLogDelegate)
                             .CreateLogger()
                             .ForContext("Source", "TempAct");
            _flushLogDelegate = flushLogDelegate;
            _timer.Elapsed += async (_, _) => await TryActCheckAsync();
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

        public async Task TryActCheckAsync()
        {
            try
            {
                await ActCheckExecAsync();
            }
            catch (Exception e)
            {
                await LogSeverity.Error.LogExceptionAsync("TempAct",
                    "Something went wrong with the TempAct cycle, continuing.", e);
                _verboseLogger.Information(e, "Something went wrong with the TempAct cycle, continuing");
            }
        }

        /// <summary>
        /// Initialize the act check and perform sanity checks
        /// </summary>
        public Task ActCheckExecAsync()
        {
            if (!ActiveChecker?.CheckCompletion() ?? false)
                return Task.CompletedTask;
            ActiveChecker = new TempActionChecker(_client, _verboseLogger);
            CurrentInfo.Checking = true;
            var start = DateTime.UtcNow;
            var timeoutPolicy = Policy.TimeoutAsync(40, Polly.Timeout.TimeoutStrategy.Optimistic, (context, timespan, task) =>
            {
                _verboseLogger.Log(LogEventLevel.Error, "TempAct",
                    $"TempAct check canceled at {DateTime.UtcNow.Subtract(start).Humanize(2)} and through {CurrentInfo.CheckedGuilds}/{_client.Guilds.Count} guilds");
                return ResetInfo(start);
            });

            return timeoutPolicy.ExecuteAsync(async ct =>
            {
                await ActiveChecker.ExecuteAsync(ct);
                await ResetInfo(start);
            }, _shutdownToken, false);
        }

        private Task ResetInfo(DateTime start)
        {
            var execTime = DateTime.UtcNow.Subtract(start);
            CachedInfo.CheckExecutionTimes.Enqueue(execTime);
            CachedInfo.LastCheck = DateTime.UtcNow;
            CurrentInfo.Checking = false;

            return (DateTime.Now - _lastFlush > TimeSpan.FromMinutes(10)) ? _flushLogDelegate() : Task.CompletedTask;
        }
    }
}