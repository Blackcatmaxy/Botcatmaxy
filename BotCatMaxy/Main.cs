﻿using Serilog.Sinks.SystemConsole.Themes;
using System.Collections.Generic;
using MongoDB.Bson.Serialization;
using System.Threading.Tasks;
using System.Globalization;
using System.Reflection;
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
        private static DiscordSocketClient _client;
        public static MongoClient dbClient;
        public static async Task Main(string[] args) {
            Utilities.logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                .WriteTo.File($"{AppDomain.CurrentDomain}/log.txt", rollingInterval: RollingInterval.Day)
#if DEBUG
                .WriteTo.File($"C:/Users/bobth/Documents/Bmax-test/log.txt", rollingInterval: RollingInterval.Day)
#endif
                .CreateLogger();
#if DEBUG
            BotInfo.debug = true;
            Utilities.BasePath = @"C:\Users\bobth\Documents\Bmax-test";
            dbClient = new MongoClient(HiddenInfo.debugDB);
#endif
            var config = new DiscordSocketConfig {
                AlwaysDownloadUsers = true,
                ConnectionTimeout = 6000,
                MessageCacheSize = 120,
                ExclusiveBulkDelete = false,
                DefaultRetryMode = RetryMode.AlwaysRetry
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

            if (args.Length > 1 && args[1].NotEmpty() && args[1].ToLower() == "canary") {
                await _client.LoginAsync(TokenType.Bot, HiddenInfo.testToken);
                dbClient = new MongoClient(HiddenInfo.debugDB);
                BotInfo.debug = true;
            } else {
#if DEBUG
                await _client.LoginAsync(TokenType.Bot, HiddenInfo.testToken);
#else
                dbClient ??= new MongoClient(HiddenInfo.mainDB);
                await _client.LoginAsync(TokenType.Bot, HiddenInfo.mainToken);
#endif
            }
            await new LogMessage(LogSeverity.Info, "Mongo", $"Connected to cluster {dbClient.Cluster.ClusterId} with {dbClient.ListDatabases().ToList().Count} databases").Log();
            await _client.StartAsync();

            //Gets build date
            const string BuildVersionMetadataPrefix = "+build";
            DateTime buildDate = new DateTime();
            var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null) {
                var value = attribute.InformationalVersion;
                var index = value.IndexOf(BuildVersionMetadataPrefix);
                if (index > 0) {
                    value = value.Substring(index + BuildVersionMetadataPrefix.Length);
                    if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)) {
                        buildDate = result.ToLocalTime();
                    }
                }
            }
            if (args.Length > 0 && args[0].NotEmpty()) {
                await new LogMessage(LogSeverity.Info, "Main", $"Starting with version {args[0]} built {buildDate.ToShortDateString()}, {(DateTime.Now - buildDate).LimitedHumanize()} ago").Log();
                await _client.SetGameAsync("version " + args[0]);
            } else {
                await new LogMessage(LogSeverity.Info, "Main", $"Starting with no version num built {buildDate.ToShortDateString()}, {(DateTime.Now - buildDate).LimitedHumanize()} ago").Log();
            }

            var serviceConfig = new CommandServiceConfig {
                DefaultRunMode = RunMode.Async,
                IgnoreExtraArgs = true
            };

            CommandService service = new CommandService(serviceConfig);
            CommandHandler handler = new CommandHandler(_client, service);

            Logging logger = new Logging(_client);
            TempActions tempActions = new TempActions(_client);
            Filter filter = new Filter(_client);

            //Debug info
            await new LogMessage(LogSeverity.Info, "Main", "Setup complete").Log();

            await Task.Delay(-1);
        }

        private static async Task Ready() {
            _client.Ready -= Ready;
            BotInfo.user = _client.CurrentUser;
            SocketGuild guild = _client.GetGuild(285529027383525376);
            BotInfo.logChannel = guild.GetTextChannel(593128958552309761);

            await new LogMessage(LogSeverity.Info, "Ready", "Running in " + _client.Guilds.Count + " guilds!").Log();
        }
    }

    public static class BotInfo {
        public static SocketTextChannel logChannel;
        public static bool debug = false;
        public static ISelfUser user;
    }
}