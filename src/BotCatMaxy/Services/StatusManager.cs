using System;
using Discord;
using Discord.WebSocket;
using System.Threading;
using System.Threading.Tasks;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

namespace BotCatMaxy.Services;

public class StatusManager : DiscordClientService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger _logger;
    private readonly string _version;
    private ushort _statusPos = 0;
    private Timer _timer;

    public StatusManager(DiscordSocketClient client, ILogger<StatusManager> logger, IConfiguration configuration) : base(client, logger)
    {
        _client = client;
        _version = configuration["version"] ?? "unknown";
        _logger = Log.ForContext("Source", "Status");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _client.WaitForReadyAsync(stoppingToken);
        _logger.Information("Statuses are running");
        _timer = new Timer(async (_) => await CheckStatus());
        _timer.Change(0, 30000);
    }

    public Task CheckStatus()
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
                    _logger.Error("Reached invalid status");
                    break;
            }
            return _client.SetGameAsync(status);

        }
        catch (Exception e)
        {
            _logger.Error(e, "Something went wrong setting status");
        }
        return Task.CompletedTask;
    }
}