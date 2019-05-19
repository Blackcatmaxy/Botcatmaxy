using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.IO;

namespace BotCatMaxy {
    public class MainClass {
        private DiscordSocketClient _client;

        public static void Main(string[] args)
            => new MainClass().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync() {
            var config = new DiscordSocketConfig {
                AlwaysDownloadUsers = true,
                MessageCacheSize = 120
            };

            //Sets up the events
            _client = new DiscordSocketClient(config);
            Filter.client = _client;
            _client.Log += Log;
            _client.MessageReceived += Filter.CheckMessage;
            await _client.LoginAsync(TokenType.Bot, HiddenInfo.token);
            await _client.StartAsync();

            CommandService service = new CommandService();
            CommandHandler handler = new CommandHandler(_client, service);

            Logging logger = new Logging(_client);
            _ = TempBanChecker.Timer(_client);

            await _client.SetGameAsync("version 0.5.5");

            await handler.InstallCommandsAsync();
            logger.SetUp();

            //Debug info
            await Log(new LogMessage(LogSeverity.Info, "Main", "Setup complete"));
            if (!Directory.Exists("/home/bob_the_daniel/Data")) {
                Console.WriteLine(DateTime.Now.TimeOfDay + " No data folder");
            }

            await Log(new LogMessage(LogSeverity.Info, "Info", "Running in " + _client.Guilds.Count + " guilds!"));
            // Block this task until the program is closed.
            await Task.Delay(-1);
            await _client.SetGameAsync("shutting down");
        }

        private Task Log(LogMessage msg) {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
