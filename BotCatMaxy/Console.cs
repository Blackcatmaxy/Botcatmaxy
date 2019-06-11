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
                await new LogMessage(LogSeverity.Info, "Console", "Messaging guild owners: " + splitInput).Log();
            }

            _ = NewInput();
        }
    }
}
