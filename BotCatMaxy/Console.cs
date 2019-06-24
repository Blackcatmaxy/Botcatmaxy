using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using System.Linq;
using System.Text;
using BotCatMaxy;
using Discord;
using System;


namespace BotCatMaxy {
    public class ConsoleReader {
        readonly DiscordSocketClient client;
        public ConsoleReader(DiscordSocketClient client) {
            this.client = client;
            _ = NewInput();
        }

        public async Task NewInput() {
            string input = Console.ReadLine();
            List<string> splitInput = input.Split(' ').ToList();

            if (splitInput[0].ToLower() == "messageowners") {
                splitInput.RemoveAt(0);
                string message = "";
                foreach (string word in splitInput) {
                    message += " " + word;
                }
                await new LogMessage(LogSeverity.Info, "Console", "Messaging guild owners:" + message).Log();
            }
            if (splitInput[0].ToLower() == "checktempbans") {
                await TempActions.TempBanChecker(client);
            }
            if (splitInput[0].ToLower() == "shutdown" || input.ToLower() == "shut down") {
                await client.SetGameAsync("Restarting");
                Environment.Exit(0);
            }
            if (splitInput[0].ToLower() == "stats") {
                Console.WriteLine("Part of " + client.Guilds.Count + " discord guilds");
            }

            _ = NewInput();
        }
    }
}
