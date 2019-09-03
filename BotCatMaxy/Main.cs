using System.Timers;
using System;
using Serilog;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.IO;
using MongoDB;
using MongoDB.Driver;
using BotCatMaxy.Settings;
using BotCatMaxy.Data;
using MongoDB.Bson.Serialization;
using System.Collections.Generic;

namespace BotCatMaxy {
    public class MainClass {
        private DiscordSocketClient _client;
        public static MongoClient dbClient;
        public static void Main(string[] args) {
#if DEBUG
            Utilities.BasePath = @"C:\Users\bobth\Documents\Bmax-test";
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

            Utilities.logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File($"{Utilities.BasePath}/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

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
                await (new LogMessage(LogSeverity.Critical, "Main", "Unable to map type", e)).Log();
            }
            dbClient = new MongoClient(HiddenInfo.debugDB);

            //Sets up the events
            _client = new DiscordSocketClient(config);
            _client.Log += Utilities.Log;
            _client.Ready += Ready;

            if (beCanary != null && beCanary.ToLower() == "canary") {
                await _client.LoginAsync(TokenType.Bot, HiddenInfo.testToken);
            } else {
                await _client.LoginAsync(TokenType.Bot, HiddenInfo.Maintoken);
            }

            await _client.StartAsync();

            if (version != null || version != "") {
                await (new LogMessage(LogSeverity.Info, "Main", "Starting with version " + version)).Log();
                await _client.SetGameAsync("version " + version);
            } else {
                await (new LogMessage(LogSeverity.Info, "Main", "Starting with no version num")).Log();
            }

            CommandService service = new CommandService();
            CommandHandler handler = new CommandHandler(_client, service);

            Logging logger = new Logging(_client);
            TempActions tempActions = new TempActions(_client);
            Filter filter = new Filter(_client);
            ConsoleReader consoleReader = new ConsoleReader(_client);

            //Debug info
            await new LogMessage(LogSeverity.Info, "Main", "Setup complete").Log();
            if (!Directory.Exists(Utilities.BasePath)) {
                await new LogMessage(LogSeverity.Error, "Main", "Data Folder not found").Log();
            }

            await Task.Delay(-1);
        }

        private async Task Ready() {
            await (new LogMessage(LogSeverity.Info, "Ready", "Running in " + _client.Guilds.Count + " guilds!")).Log();
        }
    }
}
