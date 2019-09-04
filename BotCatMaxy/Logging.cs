using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCatMaxy.Settings;
using Discord.WebSocket;
using BotCatMaxy.Data;
using Discord.Rest;
using System.Text;
using BotCatMaxy;
using Humanizer;
using Discord;
using System;

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
            if (message.Channel as SocketGuildChannel != null && message.MentionedRoleIds != null && message.MentionedRoleIds.Count > 0) {
                SocketGuild guild = (message.Channel as SocketGuildChannel).Guild;
                LogMessage("Role ping", message, guild, true);
            }
        }

        async Task LogEdit(Cacheable<IMessage, ulong> cachedMessage, SocketMessage newMessage, ISocketMessageChannel channel) {
            try {
                //Just makes sure that it's not logged when it shouldn't be
                if (!(channel is SocketGuildChannel)) return;
                SocketGuild guild = (channel as SocketGuildChannel).Guild;
                IMessage oldMessage = cachedMessage.GetOrDownloadAsync().Result;
                if (oldMessage.Content == newMessage.Content || newMessage.Author.IsBot || guild == null) return;
                LogSettings settings = guild.LoadFromFile<LogSettings>();
                if (settings == null || !settings.logEdits) return;
                SocketTextChannel logChannel = guild.GetChannel(settings.logChannel) as SocketTextChannel;
                if (logChannel == null) return;

                var embed = new EmbedBuilder();
                if (oldMessage.Content == null || oldMessage.Content == "") {
                    embed.AddField($"Message was edited in #{newMessage.Channel.Name} from",
                    "`This message had no text`");
                } else {
                    embed.AddField($"Message was edited in #{newMessage.Channel.Name} from",
                    oldMessage.Content.Truncate(1020));
                }
                if (newMessage.Content == null || newMessage.Content == "") {
                    embed.AddField($"Message was edited in #{newMessage.Channel.Name} to",
                    "`This message had no text`");
                } else {
                    embed.AddField($"Message was edited in #{newMessage.Channel.Name} to",
                    newMessage.Content.Truncate(1020));
                }

                embed.AddField("Message Link", "[Click Here](" + newMessage.GetJumpUrl() + ")", false);

                embed.WithFooter("ID: " + newMessage.Id)
                    .WithAuthor(newMessage.Author)
                    .WithColor(Color.Teal)
                    .WithCurrentTimestamp();

                logChannel.SendMessageAsync(embed: embed.Build()).Result.GetJumpUrl();
            } catch (Exception exception) {
                _ = new LogMessage(LogSeverity.Error, "Logging", exception.Message, exception).Log();
            }
        }

        public static string LogMessage(string reason, IMessage message, SocketGuild guild = null, bool addJumpLink = false) {
            try {
                if (message == null) return null;
                if (deletedMessagesCache == null) {
                    deletedMessagesCache = new List<ulong>();
                }
                if (deletedMessagesCache.Count > 0 && deletedMessagesCache.Contains(message.Id)) {
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

                LogSettings settings = guild?.LoadFromFile<LogSettings>();
                SocketTextChannel logChannel = guild?.GetChannel(settings.logChannel) as SocketTextChannel;
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
                    message.Content.Truncate(1020), true);
                }

                if (addJumpLink) {
                    embed.AddField("Message Link", "[Click Here](" + message.GetJumpUrl() + ")", true);
                }

                string links = "";
                if (!message.Attachments.IsNullOrEmpty()) {
                    foreach (IAttachment attachment in message.Attachments) {
                        links += " " + attachment.ProxyUrl;
                    }
                }

                embed.WithFooter("ID: " + message.Id)
                    .WithAuthor(message.Author)
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();
                string link = logChannel.SendMessageAsync(embed: embed.Build()).Result.GetJumpUrl();
                if (!links.IsNullOrEmpty()) logChannel.SendMessageAsync("The message above had these attachments:" + links);
                return link;
            } catch (Exception exception) {
                _ = new LogMessage(LogSeverity.Error, "Logging", exception.Message, exception).Log();
            }
            return null;
        }

        public static string LogWarn(IGuild guild, IUser warner, SocketGuildUser warnee, string reason, string warnLink) {
            try {
                LogSettings settings = guild.LoadFromFile<LogSettings>();
                if (settings == null || guild.GetTextChannelAsync(settings.logChannel).Result == null) return null;

                var embed = new EmbedBuilder();
                embed.WithAuthor(warner);
                if (warnee.Nickname.IsNullOrEmpty())
                    embed.AddField($"{warnee.Username} ({warnee.Id}) has been warned", "For " + reason);
                else
                    embed.AddField($"{warnee.Nickname} aka {warnee.Username} ({warnee.Id}) has been warned", "For " + reason);
                if (!warnLink.IsNullOrEmpty()) embed.AddField("Jumplink", $"[Click Here]({warnLink})");
                embed.WithColor(Color.Gold);
                embed.WithCurrentTimestamp();

                return guild.GetTextChannelAsync(settings.logChannel).Result.SendMessageAsync(embed: embed.Build()).Result.GetJumpUrl();
            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "Logging", "Error", e).Log();
            }
            return null;
        }

        public static void LogTempAct(IGuild guild, IUser warner, SocketGuildUser warnee, string actType, string reason, string warnLink, TimeSpan length) {
            try {
                LogSettings settings = guild.LoadFromFile<LogSettings>();
                if (settings == null || guild.GetTextChannelAsync(settings.logChannel).Result == null) return;

                var embed = new EmbedBuilder();
                embed.WithAuthor(warner);
                if (warnee.Nickname.IsNullOrEmpty())
                    embed.AddField($"{warnee.Username} ({warnee.Id}) has been temp-{actType}ed for {length.Humanize()}", $"Because of {reason}");
                else
                    embed.AddField($"{warnee.Nickname} aka {warnee.Username} ({warnee.Id}) has been temp-{actType}ed for {length.Humanize()}", $"Because of {reason}");
                if (!warnLink.IsNullOrEmpty()) embed.AddField("Jumplink", $"[Click Here]({warnLink})");
                embed.WithColor(Color.Red);
                embed.WithCurrentTimestamp();

                guild.GetTextChannelAsync(settings.logChannel).Result.SendMessageAsync(embed: embed.Build());
                return;
            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "Logging", "Error", e).Log();
            }
            return;
        }

        public static void LogEndTempAct(IGuild guild, IUser warnee, string actType, string reason, TimeSpan length) {
            try {
                LogSettings settings = guild.LoadFromFile<LogSettings>();
                if (settings == null || guild.GetTextChannelAsync(settings.logChannel).Result == null) return;

                var embed = new EmbedBuilder();
                embed.WithAuthor(warnee);
                if (!(warnee is SocketGuildUser) || (warnee as SocketGuildUser).Nickname.IsNullOrEmpty())
                    embed.AddField($"{warnee.Username} ({warnee.Id}) has been un{actType}ed", $"After {length.Humanize(2)}, because of {reason}");
                else
                    embed.AddField($"{(warnee as SocketGuildUser).Nickname} aka {warnee.Username} ({warnee.Id}) has been warned", "For " + reason);
                //if (!warnLink.IsNullOrEmpty()) embed.AddField("Jumplink", warnLink);
                embed.WithColor(Color.Green);
                embed.WithCurrentTimestamp();

                guild.GetTextChannelAsync(settings.logChannel).Result.SendMessageAsync(embed: embed.Build()).Result.GetJumpUrl();
                return;
            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "Logging", "Error", e).Log();
            }
            return;
        }

        private async Task LogDelete(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel) {
            try {
                if (!(channel is SocketGuildChannel)) return;
                LogMessage("Deleted message", message.GetOrDownloadAsync().Result);
            } catch (Exception exception) {
                Console.WriteLine(new LogMessage(LogSeverity.Error, "Logging", "Error", exception));
            }
            //Console.WriteLine(new LogMessage(LogSeverity.Info, "Logging", "Message deleted"));
        }
    }
}
