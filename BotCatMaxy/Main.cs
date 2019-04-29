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

            _client = new DiscordSocketClient(config);
            _client.Log += Log;
            _client.MessageReceived += MessageReceivedAsync;
            await _client.LoginAsync(TokenType.Bot, HiddenInfo.token);
            await _client.StartAsync();

            CommandService service = new CommandService();
            CommandHandler handler = new CommandHandler(_client, service);

            Logging logger = new Logging(_client);

            await _client.SetGameAsync("version 0.4.1");

            await handler.InstallCommandsAsync();
            logger.SetUp();

            await Log(new LogMessage(LogSeverity.Info, "Main", "Setup complete"));
            //Console.WriteLine(DateTime.Now.TimeOfDay + " All classes created");
            if (!Directory.Exists("/home/bob_the_daniel/Data")) {
                Console.WriteLine(DateTime.Now.TimeOfDay + " No data folder");
            }

            // Block this task until the program is closed.
            await Task.Delay(-1);
            await _client.SetGameAsync("shutting down");
        }

        private async Task MessageReceivedAsync(SocketMessage message) {
            if (message.Content == "!ping") {
                await message.Channel.SendMessageAsync("Pong!");
            }
            //Console.WriteLine("pizza");
            if (!message.Author.IsBot && message.Channel is SocketGuildChannel) {
                _ = SwearFilter.CheckMessage(message);
            }
        }

        //async Task MessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel) => Logging.LogDeleted("Deleted message", message.Value as SocketMessage);

        private Task Log(LogMessage msg) {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
