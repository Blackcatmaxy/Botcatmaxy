using BotCatMaxy.Cache;
using BotCatMaxy.Data;
using BotCatMaxy.Startup;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MongoDB.Driver;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    public class MainClass
    {
        private static DiscordSocketClient _client;
        public static MongoClient dbClient;
        public static async Task Main(string[] args)
        {
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                .WriteTo.File($"{AppDomain.CurrentDomain.BaseDirectory}/log.txt", rollingInterval: RollingInterval.Day);
            //Maps all the classes
            _ = DataManipulator.MapTypes();
#if DEBUG
            BotInfo.debug = true;
            dbClient = new MongoClient(HiddenInfo.debugDB);
#endif
            ExceptionLogging.logger = logConfig.CreateLogger();
            await new LogMessage(LogSeverity.Info, "Log", $"Program log logging at {AppDomain.CurrentDomain.BaseDirectory}").Log();
            var config = new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true, //going to keep here for new guilds added, but seems to be broken for startup per https://github.com/discord-net/Discord.Net/issues/1646
                ConnectionTimeout = 6000,
                MessageCacheSize = 120,
                ExclusiveBulkDelete = false,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                GatewayIntents = GatewayIntents.GuildBans | GatewayIntents.GuildMembers | GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.Guilds
            };

            //Sets up the events
            _client = new DiscordSocketClient(config);
            _client.Log += ExceptionLogging.Log;
            _client.Ready += Ready;

            //Delete once https://github.com/discord-net/Discord.Net/issues/1646 is fixed
            _client.GuildAvailable += HandleGuildAvailable;

            if (args.Length > 1 && args[1].NotEmpty() && args[1].ToLower() == "canary")
            {
                await _client.LoginAsync(TokenType.Bot, HiddenInfo.testToken);
                dbClient = new MongoClient(HiddenInfo.debugDB);
                BotInfo.debug = true;
            }
            else
            {
#if DEBUG
                await _client.LoginAsync(TokenType.Bot, HiddenInfo.testToken);
#else
                dbClient ??= new MongoClient(HiddenInfo.mainDB);
                await _client.LoginAsync(TokenType.Bot, HiddenInfo.mainToken);
#endif
            }
            await new LogMessage(LogSeverity.Info, "Mongo", $"Connected to cluster {dbClient.Cluster.ClusterId} with {dbClient.ListDatabases().ToList().Count} databases").Log();
            await _client.StartAsync();
            SettingsCache cacher = new SettingsCache(_client);

            //Gets build date
            const string BuildVersionMetadataPrefix = "+build";
            DateTime buildDate = new DateTime();
            var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null)
            {
                var value = attribute.InformationalVersion;
                var index = value.IndexOf(BuildVersionMetadataPrefix);
                if (index > 0)
                {
                    value = value.Substring(index + BuildVersionMetadataPrefix.Length);
                    if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
                    {
                        buildDate = result.ToUniversalTime();
                    }
                }
            }

            StatusManager statusManager;
            if (args.Length > 0 && args[0].NotEmpty())
            {
                await new LogMessage(LogSeverity.Info, "Main", $"Starting with version {args[0]}, built {buildDate.ToShortDateString()}, {(DateTime.UtcNow - buildDate).LimitedHumanize()} ago").Log();
                statusManager = new StatusManager(_client, args[0]);
            }
            else
            {
                await new LogMessage(LogSeverity.Info, "Main", $"Starting with no version num, built {buildDate.ToShortDateString()}, {(DateTime.UtcNow - buildDate).LimitedHumanize()} ago").Log();
                statusManager = new StatusManager(_client, "unknown");
            }

            var serviceConfig = new CommandServiceConfig
            {
                DefaultRunMode = RunMode.Async,
                IgnoreExtraArgs = true
            };

            CommandService service = new CommandService(serviceConfig);
            CommandHandler handler = new CommandHandler(_client, service);

            DynamicSlowmode dynamicSlowmode = new DynamicSlowmode(_client);
            LoggingHandler logger = new LoggingHandler(_client);
            TempActions tempActions = new TempActions(_client);
            FilterHandler filter = new FilterHandler(_client);

            //Debug info
            await new LogMessage(LogSeverity.Info, "Main", "Setup complete").Log();

            await Task.Delay(-1);
        }

        private static async Task Ready()
        {
            _client.Ready -= Ready;
            BotInfo.user = _client.CurrentUser;
            SocketGuild guild = _client.GetGuild(285529027383525376);
            BotInfo.logChannel = guild.GetTextChannel(593128958552309761);

            await new LogMessage(LogSeverity.Info, "Ready", "Running in " + _client.Guilds.Count + " guilds!").Log();
        }

        //Delete once https://github.com/discord-net/Discord.Net/issues/1646 is fixed
        private static async Task HandleGuildAvailable(SocketGuild guild)
            => _ = Task.Run(() => DownloadUsers(guild));

        private static async Task DownloadUsers(SocketGuild guild)
        {
            await guild.DownloadUsersAsync();
#if DEBUG
            Console.WriteLine($"Downloaded users from {guild.Name}");
#endif
        }
    }

    public static class BotInfo
    {
        public static SocketTextChannel logChannel;
        public static bool debug = false;
        public static ISelfUser user;
    }
}
