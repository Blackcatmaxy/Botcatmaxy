using Serilog.Sinks.SystemConsole.Themes;
using System.Collections.Generic;
using MongoDB.Bson.Serialization;
using System.Threading.Tasks;
using BotCatMaxy.Settings;
using Discord.WebSocket;
using Discord.Commands;
using BotCatMaxy.Data;
using MongoDB.Driver;
using Serilog;
using Discord;
using MongoDB;
using System;

namespace BotCatMaxy {
    public class MainClass {
        private DiscordSocketClient _client;
        public static MongoClient dbClient;
        public static void Main(string[] args) {
            Utilities.logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                .WriteTo.File($"{Utilities.BasePath}/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();
#if DEBUG
            Utilities.BasePath = @"C:\Users\bobth\Documents\Bmax-test";
            dbClient = new MongoClient(HiddenInfo.debugDB);
            new MainClass().MainAsync("Debug", "canary").GetAwaiter().GetResult();
#endif
            if (args.NotEmpty(1)) new MainClass().MainAsync(args[0], args[1]).GetAwaiter().GetResult();
            else if (args.NotEmpty(0)) new MainClass().MainAsync(args[0]).GetAwaiter().GetResult();
            else new MainClass().MainAsync(args[0]).GetAwaiter().GetResult();   
        }

        public async Task MainAsync(string version = null, string beCanary = null) {
            var config = new DiscordSocketConfig {
                AlwaysDownloadUsers = true,
                MessageCacheSize = 120
            };

            //Maps all the classes
            try {
                BsonClassMap.RegisterClassMap<List<Infraction>>();
                BsonClassMap.RegisterClassMap<ModerationSettings>();
                BsonClassMap.RegisterClassMap<UserInfractions>();
                BsonClassMap.RegisterClassMap<LogSettings>();
                BsonClassMap.RegisterClassMap<Infraction>();
                BsonClassMap.RegisterClassMap<TempAct>();
                BsonClassMap.RegisterClassMap<BadWord>();
            } catch (Exception e) {
                await new LogMessage(LogSeverity.Critical, "Main", "Unable to map type", e).Log();
            }
            
            //Sets up the events
            _client = new DiscordSocketClient(config);
            _client.Log += Utilities.Log;
            _client.Ready += Ready;

            if (beCanary != null && beCanary.ToLower() == "canary") {
                await _client.LoginAsync(TokenType.Bot, HiddenInfo.testToken);
                dbClient = new MongoClient(HiddenInfo.debugDB);
            } else {
                if (dbClient == null) dbClient = new MongoClient(HiddenInfo.mainDB);
                await _client.LoginAsync(TokenType.Bot, HiddenInfo.Maintoken);
            }

            await _client.StartAsync();

            if (version != null || version != "") {
                await new LogMessage(LogSeverity.Info, "Main", "Starting with version " + version).Log();
                await _client.SetGameAsync("version " + version);
            } else {
                await new LogMessage(LogSeverity.Info, "Main", "Starting with no version num").Log();
            }

            CommandService service = new CommandService();
            CommandHandler handler = new CommandHandler(_client, service);

            Logging logger = new Logging(_client);
            TempActions tempActions = new TempActions(_client);
            Filter filter = new Filter(_client);
            //ConsoleReader consoleReader = new ConsoleReader(_client);

            //Debug info
            await new LogMessage(LogSeverity.Info, "Main", "Setup complete").Log();

            await Task.Delay(-1);
        }

        private async Task Ready() {
            await new LogMessage(LogSeverity.Info, "Ready", "Running in " + _client.Guilds.Count + " guilds!").Log();
        }
    }
}