using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using System.Linq;
using System.Text;
using BotCatMaxy;
using System.IO;
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

            switch (splitInput[0].ToLower()) {
                case "messageowners":
                    splitInput.RemoveAt(0);
                    string message = "";
                    foreach (string word in splitInput) {
                        message += " " + word;
                    }
                    List<SocketUser> owners = new List<SocketUser>();
                    foreach (SocketGuild guild in client.Guilds) {
                        if (!owners.Contains(guild.Owner)) owners.Add(guild.Owner);
                    }
                    foreach (SocketUser owner in owners) {
                        try {
                            await owner.GetOrCreateDMChannelAsync().Result.SendMessageAsync(message);
                        } catch (Exception e) {
                            if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "Console", "Something went wrong notifying person", e).Log();
                        }
                    }
                    await new LogMessage(LogSeverity.Info, "Console", "Messaged guild owners:" + message).Log();
                    break;
                case "checktempbans":
                    await TempActions.TempActChecker(client);
                    await (new LogMessage(LogSeverity.Info, "Console", "Checked temp-actions")).Log();
                    break;
                case "shutdown":
                case "shut down":
                    await client.SetGameAsync("restarting");
                    Environment.Exit(0);
                    break;
                case "stats":   
                default:
                    await new LogMessage(LogSeverity.Warning, "Console", "Command not recognized").Log();
                    break;
            }

            _ = NewInput();
        }
    }
}
