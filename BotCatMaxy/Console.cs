using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using MongoDB.Bson.Serialization;
using System.Threading.Tasks;
using Discord.WebSocket;
using BotCatMaxy.Data;
using MongoDB.Bson;
using System.Linq;
using System.Text;
using BotCatMaxy;
using System.IO;
using Discord;
using System;
using MongoDB;
using MongoDB.Driver;
using static BotCatMaxy.ModerationFunctions;

namespace BotCatMaxy {
    public class ConsoleReader {
        readonly DiscordSocketClient client;
        public ConsoleReader(DiscordSocketClient client) {
            this.client = client;
            Task.Run(() => NewInput());
            _ = new LogMessage(LogSeverity.Info, "Main", "Console setup").Log();
        }

        public async Task NewInput() {
            try {
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
                            owner.TryNotify(message);
                        }
                        await new LogMessage(LogSeverity.Info, "Console", "Messaged guild owners:" + message).Log();
                        break;
                    case "checktempbans":
                        await TempActions.TempActChecker(client, true);
                        await (new LogMessage(LogSeverity.Info, "Console", "Checked temp-actions")).Log();
                        break;
                    case "shutdown":
                    case "shut down":
                        await client.SetGameAsync("restarting");
                        Environment.Exit(0);
                        break;
                    case "stats":
                    case "statistics":
                        Console.Write($"Part of {client.Guilds.Count} discord guilds ");
                        List<Infraction> infractions = new List<Infraction>();
                        ulong members = 0;
                        int i = 0;
                        foreach (SocketGuild guild in client.Guilds) {
                            if (i == 3)
                                members += (ulong)guild.MemberCount;
                            var collection = guild.GetInfractionsCollection(false);

                            if (collection != null) {
                                using (var cursor = collection.Find(new BsonDocument()).ToCursor()) {
                                    foreach (var doc in cursor.ToList()) {
                                        foreach (Infraction infraction in BsonSerializer.Deserialize<UserInfractions>(doc).infractions) {
                                            infractions.Add(infraction);
                                        }
                                    }
                                }
                            }
                        }
                        Console.Write($"with a total of {members} users. There are {infractions.Count} total infractions ");
                        InfractionInfo data = new InfractionInfo(infractions, 0, false);
                        Console.WriteLine($"with {data.infractionsToday.count} infractions given in the last 24 hours");
                        Console.Write("> ");
                        break;
                    default:
                        await new LogMessage(LogSeverity.Warning, "Console", "Command not recognized").Log();
                        break;
                }
            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "Main", "Console didn't work", e).Log();
            }
            Console.Beep();

            _ = NewInput();
        }
    }
}
