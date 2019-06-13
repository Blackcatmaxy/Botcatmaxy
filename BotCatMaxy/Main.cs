using System.Timers;
using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.IO;

namespace BotCatMaxy {
    public class MainClass {
        private DiscordSocketClient _client;
        public static void Main(string[] args) {
            if (args != null) 
            new MainClass().MainAsync(args[0]).GetAwaiter().GetResult();
            else new MainClass().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync(string version = null) {
            var config = new DiscordSocketConfig {
                AlwaysDownloadUsers = true,
                MessageCacheSize = 120
            };

            File.CreateText(Utilities.BasePath + "log.txt");

            //Sets up the events
            _client = new DiscordSocketClient(config);
            Filter.client = _client;
            _client.Log += Utilities.Log;
            _client.Ready += Ready;
            _client.MessageReceived += Filter.CheckMessage;
            await _client.LoginAsync(TokenType.Bot, HiddenInfo.token);
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

            ConsoleReader consoleReader = new ConsoleReader(_client);

            //Debug info
            _ = new LogMessage(LogSeverity.Info, "Main", "Setup complete").Log();
            if (!Directory.Exists(Utilities.BasePath)) {
                Console.WriteLine(DateTime.Now.TimeOfDay + " No data folder");
            }

            // Block this task until the program is closed.
            await Task.Delay(-1);
            await _client.SetGameAsync("shutting down");
        }

        private async Task Ready() {
            await (new LogMessage(LogSeverity.Info, "Ready", "Running in " + _client.Guilds.Count + " guilds!")).Log();
        }
    }
}
