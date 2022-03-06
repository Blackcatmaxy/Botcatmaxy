using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BotCatMaxy.Models;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BotCatMaxy.Services;

public class DataExperimentService : DiscordClientService
{
    private readonly DiscordSocketClient _client;
    private readonly DataService _dataService;
    const ulong guildID1 = 285529027383525376;
    const ulong guildID2 = 703655670536208464;
    public DataExperimentService(DiscordSocketClient client, ILogger<DiscordClientService> logger, DataService dataService) : base(client,
        logger)
    {
        _client = client;
        _dataService = dataService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        
        await _client.WaitForReadyAsync(stoppingToken);
        try
        {
            PerformTest();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void PerformTest()
    {
        var guild1 = _client.GetGuild(guildID1);
        var guild2 = _client.GetGuild(guildID2);
        Console.WriteLine("Starting data experiment");
        using (var session = _dataService.OpenSettingsSession())
        {
            var settings = new ModerationSettings()
            {
                guild = guild1,
                mutedRole = 750087728443686962
            };
            session.Store(settings);
            Console.WriteLine("Sent first modsettings");
            session.SaveChanges();
            settings = new ModerationSettings()
            {
                guild = guild2,
                mutedRole = 750087728443686962
            };
            session.Store(settings);
            
            var filterSettings = new FilterSettings
            {
                guild = guild1
                
            };
            session.Store(filterSettings);

            Console.WriteLine("Sent first filter");
            var logSettings = new LogSettings(Guild: guild1);
            session.Store(logSettings);
            
            logSettings = new LogSettings(Guild: guild2);
            session.Store(logSettings);

            using var infractionSession = _dataService.OpenInfractionSession();
            {
                long id = new Random().NextInt64(0, (long.MaxValue));
                var infractions = new UserInfractions()
                {
                    Guild = guild1,
                    userId = (ulong)id,
                    infractions = new List<Infraction>()
                    {
                        new() {LogLink = "https://discord.com/fdsfdsfds", Reason = "test", Size = 1, Time = DateTime.UtcNow}
                    }
                };
        
                session.Store(infractions);
                session.SaveChanges();
            }
            
            Console.WriteLine("Saving");
            session.SaveChanges();
            Console.WriteLine("Saved new settings");
        }
        Console.WriteLine("Disposed session");
    }
}