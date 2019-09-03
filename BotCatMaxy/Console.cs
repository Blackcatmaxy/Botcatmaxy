using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
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
using MongoDB.Bson.Serialization;

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
                    await TempActions.TempActChecker(client, true);
                    await (new LogMessage(LogSeverity.Info, "Console", "Checked temp-actions")).Log();
                    break;
                case "shutdown":
                case "shut down":
                    await client.SetGameAsync("restarting");
                    Environment.Exit(0);
                    break;
                case "stats":
                    ulong infractions = 0;
                    ulong members = 0;
                    foreach (SocketGuild guild in client.Guilds) {
                        members += (ulong)guild.MemberCount;
                        var collection = guild.GetInfractionsCollection(false);

                        if (collection != null) {
                            using (var cursor = collection.Find(new BsonDocument()).ToCursor()) {
                                foreach (var doc in cursor.ToList()) {
                                    infractions += (ulong)BsonSerializer.Deserialize<UserInfractions>(doc).infractions.Count;
                                }
                            }
                        }
                    }

                    await (new LogMessage(LogSeverity.Info, "Console", $"Part of {client.Guilds.Count} discord guilds with a total of {members} users. There are {infractions} total infractions")).Log();
                    break;
                default:
                    await new LogMessage(LogSeverity.Warning, "Console", "Command not recognized").Log();
                    break;
            }

            _ = NewInput();
        }
    }
}
