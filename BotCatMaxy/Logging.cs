using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using BotCatMaxy;
using BotCatMaxy.Settings;

namespace BotCatMaxy { 
    class Logging {
        public static List<ulong> deletedMessagesCache = new List<ulong>();
        private readonly DiscordSocketClient _client;
        public Logging(DiscordSocketClient client) {
            _client = client;
        }

        public void SetUp() {
            //Console.WriteLine(DateTime.Now.TimeOfDay + " Setup logging");
            _client.MessageDeleted += LogDelete;
        }

        public static void LogDeleted(string reason, IMessage message, SocketGuild guild = null) {
            try {
                if (deletedMessagesCache == null) {
                    deletedMessagesCache = new List<ulong>();
                }
                if (deletedMessagesCache.Contains(message.Id)) {
                    return;
                }
                if (deletedMessagesCache.Count == 5) {
                    deletedMessagesCache.RemoveAt(4);
                }
                deletedMessagesCache.Insert(0, message.Id);

                if (guild == null) {
                    Utilities.GetGuild(message.Channel as SocketGuildChannel);
                    if (guild == null) {
                        return;
                    }
                }

                LogSettings settings = SettingFunctions.LoadLogSettings(guild);
                SocketTextChannel logChannel = guild.GetChannel(settings.logChannel) as SocketTextChannel;
                if (settings == null || logChannel == null || !settings.logDeletes) {
                    return;
                }

                var embed = new EmbedBuilder();
                SocketTextChannel channel = message.Channel as SocketTextChannel;
                if (message.Embeds.Count == 0) {
                    if (message.Content == null || message.Content == "") {
                        embed.AddField(reason + " in #" + message.Channel.Name,
                        "This message had no text", true);
                    } else {
                        embed.AddField(reason + " in #" + message.Channel.Name,
                        message.Content, true);
                    }
                } else {
                    embed.AddField(reason + " in #" + channel.Name,
                    "`Embed cannot be displayed`", true);
                }
                
                embed.WithFooter("ID: " + message.Id)
                    .WithAuthor(message.Author)
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();

                _ = logChannel.SendMessageAsync(embed: embed.Build());
            } catch (Exception exception) {
                Console.WriteLine(new LogMessage(LogSeverity.Error, "Logging", exception.Message, exception));
            }
        }

        private async Task LogDelete(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel) {
            try {
                SocketGuild guild = Utilities.GetGuild(channel as SocketTextChannel);

                LogDeleted("Deleted message", message.Value, guild);
                if (message.Value.Attachments != null) {

                }
            } catch (Exception exception) {
                Console.WriteLine(new LogMessage(LogSeverity.Error, "Logging", "Error", exception));
            }
            //Console.WriteLine(new LogMessage(LogSeverity.Info, "Logging", "Message deleted"));
        }

        async Task LogEdit(Cacheable<IMessage, ulong> oldMessage, SocketMessage newMessage, ISocketMessageChannel channel) {
            new LogMessage(LogSeverity.Info, "", oldMessage.GetOrDownloadAsync().Result.Content);
        }
    }   
}
