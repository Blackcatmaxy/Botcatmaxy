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
                ulong infractions = 0;
                ulong members = 0;
                foreach (SocketGuild guild in client.Guilds) {
                    members += (ulong)guild.MemberCount;
                    string guildDir = guild.GetPath(false);

                    if (!guildDir.IsNullOrEmpty()) {
                        foreach (SocketUser user in guild.Users) {
                            if (Directory.Exists(guildDir + "/Infractions/Discord") && File.Exists(guildDir + "/Infractions/discord/" + user.Id)) {
                                BinaryFormatter newbf = new BinaryFormatter();
                                FileStream newFile = File.Open(guildDir + "/Infractions/discord/" + user.Id, FileMode.Open);
                                Infraction[] oldInfractions;
                                oldInfractions = (Infraction[])newbf.Deserialize(newFile);
                                newFile.Close();
                                foreach (Infraction infraction in oldInfractions) {
                                    infractions++;
                                }
                            }
                        }
                    }
                }

                Console.WriteLine($"Part of {client.Guilds.Count} discord guilds with a total of {members} users");
            }

            _ = NewInput();
        }
    }
}
