using System;
using System.IO;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using BotCatMaxy.Services.TempActions;
using BotCatMaxy.Services.Logging;
using BotCatMaxy.Startup;
using BotCatMaxy.Services;
using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using RunMode = Discord.Commands.RunMode;

namespace BotCatMaxy
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            const string template =
                "[{@t:HH:mm:ss} {@l:u3}]{#if IsDefined(Source)} {Concat(Source,':'),-9}{#end} {@m}{#if IsDefined(GuildID)} (from {GuildID}){#end}\n{@x}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Async(a =>
                    a.File(new ExpressionTemplate(template), "logs/log.txt",
                        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14))
                .WriteTo.Console(new ExpressionTemplate(template, theme: TemplateTheme.Code))
                .CreateLogger();

            var hostBuilder = Host.CreateDefaultBuilder()
                .UseSerilog(Log.Logger)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();
                    var path = "Properties/BotCatMaxy";
#if DEBUG
                    if (File.Exists($"{path}.DEBUG.ini"))
                        path += ".DEBUG";
#endif
                    Console.WriteLine($"Checking config for {path}.ini at {Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()!.Location)}/");
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

                    AddMongoToServices(context, services);
                    services.AddSingleton(context.Configuration);
                    services.AddSingleton<InteractiveService>();
                    services.AddSingleton<InteractionService>();
                    services.AddSingleton(new CommandService(new CommandServiceConfig
                    {
                        DefaultRunMode = RunMode.Async,
                        CaseSensitiveCommands = false,
                        IgnoreExtraArgs = true
                    }));

                    services.AddHostedService<BotInfo>();
                    services.AddHostedService<StatusManager>();
                    services.AddHostedService<FilterHandler>();
                    services.AddHostedService<LoggingHandler>();
                    services.AddHostedService<TextCommandHandler>();
                    services.AddHostedService<SlashCommandHandler>();
                    services.AddHostedService<TempActionService>();
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

        //Set up and add Mongo
        private static void AddMongoToServices(HostBuilderContext context, IServiceCollection services)
        {
            MongoClient mongo = null;
            try
            {
                mongo = new MongoClient(context.Configuration["DataToken"]);
            }
            catch
            {
                LogSeverity.Critical.Log("Data",
                    "Mongo connection not successful, is BotCatMaxy.ini created? Follow Properties/Template.ini for instructions.");

                return;
            }
            DataManipulator.dbClient = mongo;
            DataManipulator.MapTypes();
            services.AddSingleton(mongo);
        }
    }
}