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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    public class MainClass
    {
        private static DiscordSocketClient _client;
        public static IMongoClient dbClient;
        public static async Task Main(string[] args)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                .WriteTo.File($"{baseDir}/log.txt", rollingInterval: RollingInterval.Day);
            ExceptionLogging.logger = logConfig.CreateLogger();
            await new LogMessage(LogSeverity.Info, "App", $"Starting with logging at {baseDir}").Log();

            await DataManipulator.MapTypes();
#if DEBUG
            BotInfo.debug = true;
#endif
            DotNetEnv.Env.Load($"{baseDir}/BotCatMaxy.env");
            dbClient = new MongoClient(Environment.GetEnvironmentVariable("DataToken"));

            var config = new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true, //going to keep here for new guilds added, but seems to be broken for startup per https://github.com/discord-net/Discord.Net/issues/1646
                ConnectionTimeout = 6000,
                MessageCacheSize = 120,
                ExclusiveBulkDelete = false,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                GatewayIntents = GatewayIntents.GuildBans | GatewayIntents.GuildMembers |
                    GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.Guilds
            };

            //Sets up the events
            _client = new DiscordSocketClient(config);
            _client.Log += ExceptionLogging.Log;
            _client.Ready += Ready;

            //Delete once https://github.com/discord-net/Discord.Net/issues/1646 is fixed
            _client.GuildAvailable += HandleGuildAvailable;

            await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DiscordToken"));
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

            string version = args.ElementAtOrDefault(0) ?? "unknown";
            await new LogMessage(LogSeverity.Info, "Main", $"Starting with version {version}, built {buildDate.ToShortDateString()}, {(DateTime.UtcNow - buildDate).LimitedHumanize()} ago").Log();
            StatusManager statusManager = new StatusManager(_client, version);

            await new LogMessage(LogSeverity.Info, "Mongo", $"Connected to cluster {dbClient.Cluster.ClusterId} with {dbClient.ListDatabases().ToList().Count} databases").Log();

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
