using System;
using System.IO;
using System.Threading.Tasks;
using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Startup;
using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;

namespace BotCatMaxy
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Async(a => 
                    a.File("logs/log.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14))
                .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                .CreateLogger();
            
            var hostBuilder = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();
                    var path = "Properties/BotCatMaxy";
#if DEBUG
                    if (File.Exists($"{path}.DEBUG.ini"))
                        path += ".DEBUG";                    
#endif
                    Console.WriteLine($"Loading config from {path}.ini");
                    config.AddIniFile($"{path}.ini", optional: true, reloadOnChange: true);
                })
                .ConfigureDiscordHost((context, config) =>
                {
                    config.SocketConfig = new DiscordSocketConfig
                    {
                        AlwaysDownloadUsers =
                            true, //going to keep here for new guilds added, but seems to be broken for startup per https://github.com/discord-net/Discord.Net/issues/1646
                        ConnectionTimeout = 6000,
                        MessageCacheSize = 120,
                        ExclusiveBulkDelete = false,
                        LogLevel = LogSeverity.Info,
                        DefaultRetryMode = RetryMode.AlwaysRetry,
                        GatewayIntents = GatewayIntents.GuildBans | GatewayIntents.GuildMembers |
                                         GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMessages |
                                         GatewayIntents.DirectMessages | GatewayIntents.Guilds
                    };

                    var token = context.Configuration["DiscordToken"];
                    if (string.IsNullOrEmpty(token))
                    {
                        Console.WriteLine("Discord token missing. Check src/BotCatMaxy/Properties/Template.ini for info.");
                        return;
                    }

                    config.Token = token;
                })
                .ConfigureServices((context, services) =>
                {
                    //Tell Dependency Injection that it can put DiscordSocketClient where IDiscordClient is requested 
                    services.AddSingleton<IDiscordClient, DiscordSocketClient>(x => x.GetRequiredService<DiscordSocketClient>());
                    //Set up and add Mongo
                    var mongo = new MongoClient(context.Configuration["DataToken"]);
                    DataManipulator.dbClient = mongo;
                    services.AddSingleton(mongo);
                    
                    services.AddSingleton(x =>
                        new InteractivityService(x.GetRequiredService<DiscordSocketClient>()));
                    
                    services.AddHostedService<BotInfo>();
                    services.AddHostedService<CommandHandler>();
                })
                .UseCommandService((context, config) =>
                {
                    config.LogLevel = LogSeverity.Verbose;
                    config.DefaultRunMode = RunMode.Async;
                    config.CaseSensitiveCommands = false;
                    config.IgnoreExtraArgs = true;
                });
            
            try
            {
                //Throws OptionsValidationException when Discord token isn't valid
                await hostBuilder.RunConsoleAsync();
            }
            catch (OptionsValidationException) //Spent way too long figuring this out, should save time in future
            {
                await new LogMessage(LogSeverity.Critical, "Discord", "Invalid Discord Token").Log();
            }
        }
    }
}