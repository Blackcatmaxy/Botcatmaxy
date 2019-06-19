using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCatMaxy.Settings;
using Discord.WebSocket;
using Discord.Commands;
using BotCatMaxy.Data;
using System.Linq;
using BotCatMaxy;
using System.IO;
using Discord;
using System;

namespace BotCatMaxy {
    public class Filter {
        readonly DiscordSocketClient client;
        public Filter(DiscordSocketClient client) {
            this.client = client;
            client.MessageReceived += CheckMessage;
            client.MessageUpdated += CheckEdit;
            new LogMessage(LogSeverity.Info, "Filter", "Filter is active").Log();
        }

        public async Task CheckEdit(Cacheable<IMessage, ulong> oldMessage, SocketMessage editedMessage, ISocketMessageChannel channel) {
            _ = CheckMessage(editedMessage);
        }

        public async Task CheckMessage(SocketMessage message) {
            try {
                if (message.Author.IsBot && !(message.Channel is SocketGuildChannel)) {
                    return; //Makes sure it's not logging a message from a bot and that it's in a discord server
                }
                SocketCommandContext context = new SocketCommandContext(client, message as SocketUserMessage);
                var chnl = message.Channel as SocketGuildChannel;
                var Guild = chnl.Guild;
                string guildDir = Guild.GetPath();

                if (Guild != null && Directory.Exists(guildDir) && !Utilities.HasAdmin(message.Author as SocketGuildUser)) {
                    ModerationSettings modSettings = Guild.LoadModSettings(false);
                    List<BadWord> badWords = Guild.LoadBadWords();

                    if (modSettings != null) {
                        if (modSettings.channelsWithoutAutoMod != null && modSettings.channelsWithoutAutoMod.Contains(chnl.Id) || !(message.Author as SocketGuildUser).CanBeWarned()) {
                            return; //Returns if channel is set as not using automod
                        }
                        //Checks if a message contains an invite
                        if (message.Content.ToLower().Contains("discord.gg/") || message.Content.ToLower().Contains("discordapp.com/invite/")) {
                            if (!modSettings.invitesAllowed) {
                                _ = ((SocketGuildUser)message.Author).Warn(0.5f, "Posted Invite", context);
                                await message.Channel.SendMessageAsync(message.Author.Mention + " has been given their " + (message.Author as SocketGuildUser).LoadInfractions("Discord").Count.Suffix() + " infraction because of posting a discord invite");

                                Logging.LogMessage("Bad word removed", message, Guild);
                                await message.DeleteAsync();
                                return;
                            }
                        }
                        if (modSettings.allowedLinks != null && modSettings.allowedLinks.Count > 0) {
                            const string linkRegex = @"^((?:https?|steam):\/\/[^\s<]+[^<.,:;" + "\"\'\\]\\s])";
                            MatchCollection matches = Regex.Matches(message.Content, linkRegex, RegexOptions.IgnoreCase);
                            if (matches != null && matches.Count > 0) await new LogMessage(LogSeverity.Info, "Filter", "Link detected").Log();
                            foreach (Match match in matches) {
                                if (!modSettings.allowedLinks.Any(s => match.ToString().ToLower().Contains(s.ToLower()))) {
                                    await ((SocketGuildUser)message.Author).Warn(1, "Using unauthorized links", context);
                                    await message.Channel.SendMessageAsync(message.Author.Mention + " has been given their " + (message.Author as SocketGuildUser).LoadInfractions("Discord").Count.Suffix() + " infraction because of using unauthorized links");

                                    Logging.LogMessage("Bad link removed", message, Guild);
                                    await message.DeleteAsync();
                                    break;
                                }
                            }
                        }
                    }

                    if (File.Exists(guildDir + "/badwords.json")) {
                        foreach (BadWord badWord in badWords) {
                            if (badWord.partOfWord) {
                                if (message.Content.ToLower().Contains(badWord.word.ToLower())) {
                                    if (badWord.euphemism != null && badWord.euphemism != "") {
                                        await ((SocketGuildUser)message.Author).Warn(0.5f, "Bad word used (" + badWord.euphemism + ")", context);
                                    } else {
                                        await ((SocketGuildUser)message.Author).Warn(0.5f, "Bad word usage", context);
                                    }
                                    await message.Channel.SendMessageAsync(message.Author.Mention + " has been given their " + (message.Author as SocketGuildUser).LoadInfractions("Discord").Count.Suffix() + " infraction because of using a bad word");

                                    Logging.LogMessage("Bad word removed", message, Guild);
                                    await message.DeleteAsync();
                                    return;
                                }
                            } else {
                                string[] messageParts = message.Content.Split(' ');
                                if (message.Content.ToLower() == badWord.word.ToLower()) {
                                    if (badWord.euphemism != null && badWord.euphemism != "") {
                                        await ((SocketGuildUser)message.Author).Warn(0.5f, "Bad word used (" + badWord.euphemism + ")", context);
                                    } else {
                                        await ((SocketGuildUser)message.Author).Warn(0.5f, "Bad word usage", context);
                                    }
                                    await message.Channel.SendMessageAsync(message.Author.Mention + " has been given their " + (message.Author as SocketGuildUser).LoadInfractions("Discord").Count.Suffix() + " infraction because of using a bad word");

                                    Logging.LogMessage("Bad word removed", message, Guild);
                                    await message.DeleteAsync();
                                    return;
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "Filter", "Something went wrong with the filter", e).Log();
            }
        }
    }
}
