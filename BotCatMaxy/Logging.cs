using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using BotCatMaxy;
using BotCatMaxy.Settings;
using BotCatMaxy.Data;

namespace BotCatMaxy {
    class Logging {
        public static List<ulong> deletedMessagesCache = new List<ulong>();
        private readonly DiscordSocketClient _client;
        public Logging(DiscordSocketClient client) {
            _client = client;

            _ = SetUpAsync();
        }

        public async Task SetUpAsync() {
            _client.MessageDeleted += LogDelete;
            _client.MessageUpdated += LogEdit;
            _client.MessageReceived += LogNew;

            await new LogMessage(LogSeverity.Info, "Logs", "Logging set up").Log();
        }

        async Task LogNew(IMessage message) {
            SocketGuild guild = (message.Channel as SocketGuildChannel).Guild;
            if (guild != null && message.MentionedRoleIds != null && message.MentionedRoleIds.Count > 0) {
                LogMessage("Role ping", message, guild, true);
            }
        }

        async Task LogEdit(Cacheable<IMessage, ulong> oldMessage, SocketMessage newMessage, ISocketMessageChannel channel) {
            if (!newMessage.Author.IsBot && channel is SocketGuildChannel) LogMessage("Message was edited from", oldMessage.GetOrDownloadAsync().Result, addJumpLink: true);
        }

        public static string LogMessage(string reason, IMessage message, SocketGuild guild = null, bool addJumpLink = false) {
            try {
                if (deletedMessagesCache == null) {
                    deletedMessagesCache = new List<ulong>();
                }
                if (deletedMessagesCache.Contains(message.Id)) {
                    return null;
                }
                if (deletedMessagesCache.Count == 5) {
                    deletedMessagesCache.RemoveAt(4);
                }
                deletedMessagesCache.Insert(0, message.Id);

                if (guild == null) {
                    guild = Utilities.GetGuild(message.Channel as SocketGuildChannel);
                    if (guild == null) {
                        return null;
                    }
                }

                LogSettings settings = guild.LoadLogSettings();
                SocketTextChannel logChannel = guild.GetChannel(settings.logChannel) as SocketTextChannel;
                if (settings == null || logChannel == null || !settings.logDeletes) {
                    return null;
                }

                var embed = new EmbedBuilder();
                SocketTextChannel channel = message.Channel as SocketTextChannel;
                if (message.Content == null || message.Content == "") {
                    embed.AddField(reason + " in #" + message.Channel.Name,
                    "`This message had no text`", true);
                } else {
                    embed.AddField(reason + " in #" + message.Channel.Name,
                    message.Content, true);
                }

                if (addJumpLink) {
                    embed.AddField("Message Link", "[Click Here](" + message.GetJumpUrl() + ")", true);
                }

                embed.WithFooter("ID: " + message.Id)
                    .WithAuthor(message.Author)
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();

                return logChannel.SendMessageAsync(embed: embed.Build()).Result.GetJumpUrl();
            } catch (Exception exception) {
                _ = new LogMessage(LogSeverity.Error, "Logging", exception.Message, exception).Log();
            }
            return null;
        }

        public static string LogWarn(IGuild guild, IUser warner, SocketGuildUser warnee, string reason) {
            try {
                LogSettings settings = guild.LoadLogSettings(false);
                if (settings == null || guild.GetTextChannelAsync(settings.logChannel).Result == null) return null;

                var embed = new EmbedBuilder();
                embed.WithAuthor(warner);
                if (warnee.Nickname.IsNullOrEmpty()) 
                    embed.AddField($"{warnee.Username} ({warnee.Id}) has been warned", "For " + reason);
                else
                    embed.AddField($"{warnee.Nickname} aka {warnee.Username} ({warnee.Id}) has been warned", "For " + reason);
                embed.WithColor(Color.Gold);

                return guild.GetTextChannelAsync(settings.logChannel).Result.SendMessageAsync(embed: embed.Build()).Result.GetJumpUrl();
            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "Logging", "Error", e).Log();
            }
            return null;
        }

        private async Task LogDelete(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel) {
            try {
                SocketGuild guild = Utilities.GetGuild(channel as SocketTextChannel);

                LogMessage("Deleted message", message.Value, guild);
                if (message.Value.Attachments != null) {

                }
            } catch (Exception exception) {
                Console.WriteLine(new LogMessage(LogSeverity.Error, "Logging", "Error", exception));
            }
            //Console.WriteLine(new LogMessage(LogSeverity.Info, "Logging", "Message deleted"));
        }
    }
}
