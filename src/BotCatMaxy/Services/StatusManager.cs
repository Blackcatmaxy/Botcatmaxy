using System;
using Discord;
using Discord.WebSocket;
using System.Threading;
using System.Threading.Tasks;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BotCatMaxy
{
    public class StatusManager : DiscordClientService
    {
        private readonly DiscordSocketClient _client;
        private readonly string _version;
        private ushort _statusPos = 0;
        private Timer _timer;

        public StatusManager(DiscordSocketClient client, ILogger<DiscordClientService> logger, IConfiguration configuration) : base(client, logger)
        {
            _client = client;
            _version = configuration["version"] ?? "unknown";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _client.WaitForReadyAsync(stoppingToken);
            await new LogMessage(LogSeverity.Info, "Status", "Statuses are running").Log();
            _timer = new Timer(async (_) => await CheckStatus());
            _timer.Change(0, 30000);
        }

        public async Task CheckStatus()
        {
            try
            {
                string status = null;
                switch (_statusPos)
                {
                    case 0:
                        status = $"version {_version}";
                        _statusPos++;
                        break;
                    case 1:
                        status = "with info at https://bot.blackcatmaxy.com";
                        _statusPos++;
                        break;
                    case 2:
                        status = "Donate at https://donate.blackcatmaxy.com to help keep the bot running";
                        _statusPos = 0;
                        break;
                    default:
                        await new LogMessage(LogSeverity.Error, "Status", "Reached invalid status").Log();
                        break;
                }
                await _client.SetGameAsync(status);
            }
            catch (Exception e)
            {
                await LogSeverity.Error.LogExceptionAsync("Status", "Something went wrong setting status", e);
            }
        }
    }
}
