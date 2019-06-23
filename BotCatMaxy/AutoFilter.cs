using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCatMaxy.Settings;
using Discord.WebSocket;
using Discord.Commands;
using BotCatMaxy.Data;
using Newtonsoft.Json;
using System.Text;
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
                if (message.Author.IsBot || !(message.Channel is SocketGuildChannel)) {
                    return; //Makes sure it's not logging a message from a bot and that it's in a discord server
                }
                SocketCommandContext context = new SocketCommandContext(client, message as SocketUserMessage);
                var chnl = message.Channel as SocketGuildChannel;
                var Guild = chnl.Guild;
                string guildDir = Guild.GetPath();

                if (Guild == null || !Directory.Exists(guildDir)) return;
                ModerationSettings modSettings = Guild.LoadModSettings(false);
                List<BadWord> badWords = Guild.LoadBadWords();

                if (modSettings != null) {
                    if (modSettings.channelsWithoutAutoMod != null && modSettings.channelsWithoutAutoMod.Contains(chnl.Id) || (message.Author as SocketGuildUser).CantBeWarned())
                        return; //Returns if channel is set as not using automod

                    //Checks if a message contains an invite
                    if (!modSettings.invitesAllowed && message.Content.ToLower().Contains("discord.gg/") || message.Content.ToLower().Contains("discordapp.com/invite/")) {
                        _ = ((SocketGuildUser)message.Author).Warn(0.5f, "Posted Invite", context);
                        await message.Channel.SendMessageAsync(message.Author.Mention + " has been given their " + (message.Author as SocketGuildUser).LoadInfractions("Discord").Count.Suffix() + " infraction because of posting a discord invite");

                        Logging.LogMessage("Bad word removed", message, Guild);
                        await message.DeleteAsync();
                        return;
                    }

                    //Checks for links
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
                                return;
                            }
                        }
                    }
                }

                //Checks for bad words
                if (File.Exists(guildDir + "/badwords.json")) {
                    StringBuilder sb = new StringBuilder();
                    foreach (char c in message.Content) {
                        if (!char.IsPunctuation(c) && !char.IsSymbol(c)) sb.Append(c);
                    }
                    string strippedMessage = sb.ToString().ToLower();

                    foreach (BadWord badWord in badWords) {
                        if (badWord.partOfWord) {
                            if (strippedMessage.Contains(badWord.word.ToLower())) {
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
                            string[] messageParts = strippedMessage.Split(' ');
                            foreach (string word in messageParts) {
                                if (word == badWord.word.ToLower()) {
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

    [Group("automod")]
    [Alias("auto-mod", "filter")]
    [RequireContext(ContextType.Guild)]
    public class FilterSettingCommands : ModuleBase<SocketCommandContext> {
        [Command("list")]
        [Alias("info")]
        [RequireContext(ContextType.Guild)]
        public async Task ListAutoMod(string extension = "", [Remainder] string whoCaresWhatGoesHere = null) {
            try {
                ModerationSettings settings = Context.Guild.LoadModSettings(false);
                BadWords badWords = new BadWords(Context.Guild);

                var embed = new EmbedBuilder();
                string message = "";

                bool useExplicit = false;
                if (extension != null && extension.ToLower() == "explicit" || extension.ToLower() == "e") {
                    if ((Context.Message.Author as SocketGuildUser).CanWarn()) {
                        useExplicit = true;
                    } else {
                        await ReplyAsync("You lack the permissions for viewing explicit bad words");
                        return;
                    }
                }

                if (settings == null) {
                    embed.AddField("Moderation settings", "Are null", true);
                } else {
                    if (settings.allowedLinks == null || settings.allowedLinks.Count == 0) {
                        embed.AddField("Allowed links", "Links aren't moderated  ", true);
                    } else {
                        message = "";
                        foreach (string link in settings.allowedLinks) {
                            if (message != "") {
                                message += "  \n";
                            }
                            message += link;
                        }

                        embed.AddField("Allowed links", message, true);
                    }
                    embed.AddField("Warn for posting invite", !settings.invitesAllowed, true);
                }

                message = "";
                if (badWords != null && badWords.all != null && badWords.all.Count > 0) {
                    foreach (BadWord badWord in badWords.all) {
                        if (message != "") {
                            message += "\n";
                        }
                        if (badWord.euphemism != null) message += badWord.euphemism;
                        if (useExplicit) message += " (" + badWord.word + ")";
                        if (badWord.partOfWord) {
                            message += "⌝";
                        }
                        message += "  ";
                    }
                    embed.AddField("Badword euphemisms", message, false);
                }

                message = "The symbol '⌝' next to a word means that you can be warned for a word that contains the bad word";
                IDMChannel channel = Context.Message.Author.GetOrCreateDMChannelAsync().Result;
                if (channel != null) {
                    _ = channel.SendMessageAsync(message, embed: embed.Build());
                } else {
                    _ = ReplyAsync(Context.Message.Author.Mention + " we can't send a message to your DMs");
                }
            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "Settings", "Error", e).Log();
            }
        }

        [Command("ToggleContainBadWord")]
        [Alias("togglecontainword")]
        [HasAdmin()]
        public async Task ToggleContainBadWord(string word) {
            BadWords badWords = new BadWords(Context.Guild);
            foreach (BadWord badWord in badWords.all) {
                if (badWord.word.ToLower() == word.ToLower()) {
                    if (badWord.partOfWord) {
                        badWord.partOfWord = false;
                        await ReplyAsync("Set badword to not be filtered if it's inside of another word");
                    } else {
                        badWord.partOfWord = true;
                        await ReplyAsync("Set badword to be filtered even if it's inside of another word");
                    }

                    badWords.all.SaveBadWords(Context.Guild);
                    return;
                }
            }
            await ReplyAsync("Badword not found");
        }

        [Command("channeltoggle")]
        [HasAdmin]
        public async Task ToggleAutoMod() {
            ModerationSettings settings = Context.Guild.LoadModSettings(true);

            if (settings.channelsWithoutAutoMod.Contains(Context.Channel.Id)) {
                settings.channelsWithoutAutoMod.Remove(Context.Channel.Id);
                await ReplyAsync("Enabled automod in this channel");
            } else {
                settings.channelsWithoutAutoMod.Add(Context.Channel.Id);
                await ReplyAsync("Disabled automod in this channel");
            }

            settings.SaveModSettings(Context.Guild);
        }

        [Command("addignoredrole")]
        [HasAdmin]
        public async Task AddWarnIgnoredRole(SocketRole role) {
            ModerationSettings settings = Context.Guild.LoadModSettings(true);
            if (settings.cantBeWarned == null) settings.cantBeWarned = new List<ulong>();
            else if (settings.cantBeWarned.Contains(role.Id)) {
                await ReplyAsync($"Role '{role.Name}' is already not able to be warned");
                return;
            }
            settings.cantBeWarned.Add(role.Id);
            settings.SaveModSettings(Context.Guild);
            await ReplyAsync($"Role '{role.Name}' will not be able to be warned now");
        }

        [Command("removeignoredrole")]
        [HasAdmin]
        public async Task RemovedWarnIgnoredRole(SocketRole role) {
            ModerationSettings settings = Context.Guild.LoadModSettings(false);
            if (settings == null || settings.cantBeWarned == null) settings.cantBeWarned = new List<ulong>();
            else if (settings.cantBeWarned.Contains(role.Id)) {
                await ReplyAsync($"Role '{role.Name}' is already able to be warned");
            } else {
                settings.cantBeWarned.Add(role.Id);
                settings.SaveModSettings(Context.Guild);
                await ReplyAsync($"Role '{role.Name}' will not be able to be warned now");
            }
        }

        [Command("addallowedlink")]
        [HasAdmin]
        public async Task AddAllowedLink(string link) {
            ModerationSettings settings = Context.Guild.LoadModSettings(true);
            if (settings.allowedLinks == null) settings.allowedLinks = new List<string>();
            settings.allowedLinks.Add(link);
            settings.SaveModSettings(Context.Guild);
            await ReplyAsync("People will now be allowed to use " + link);
        }

        [Command("removeallowedlink")]
        [HasAdmin]
        public async Task RemoveAllowedLink(string link) {
            ModerationSettings settings = Context.Guild.LoadModSettings(true);
            if (settings.allowedLinks == null || !settings.allowedLinks.Contains(link)) {
                await ReplyAsync("Link is already not allowed");
                return;
            }
            settings.allowedLinks.Remove(link);
            if (settings.allowedLinks.Count == 0) settings.allowedLinks = null;
            settings.SaveModSettings(Context.Guild);
            await ReplyAsync("People will no longer be allowed to use " + link);
        }

        [Command("toggleinvitewarn")]
        [HasAdmin]
        public async Task ToggleInviteWarn() {
            IUserMessage message = await ReplyAsync("Trying to toggle");
            ModerationSettings settings = Context.Guild.LoadModSettings(true);

            if (settings == null) {
                settings = new ModerationSettings();
                Console.WriteLine(DateTime.Now.ToShortTimeString() + " Creating new mod settings");
            }
            settings.invitesAllowed = !settings.invitesAllowed;
            Console.WriteLine(DateTime.Now.ToShortTimeString() + " setting invites to " + settings.invitesAllowed);

            settings.SaveModSettings(Context.Guild);

            await message.ModifyAsync(msg => msg.Content = "set invites allowed to " + settings.invitesAllowed.ToString().ToLower());
        }

        [Command("removeword")]
        [Alias("removebadword")]
        [HasAdmin]
        public async Task RemoveBadWord(string word) {
            List<BadWord> badWords = Context.Guild.LoadBadWords(); ;

            if (badWords == null) {
                await ReplyAsync("Bad words is null");
                return;
            }
            BadWord badToRemove = null;
            foreach (BadWord badWord in badWords) {
                if (badWord.word == word) {
                    badToRemove = badWord;
                }
            }
            if (badToRemove != null) {
                badWords.Remove(badToRemove);
                badWords.SaveBadWords(Context.Guild);

                await ReplyAsync("removed " + word + " from bad word list");
            } else {
                await ReplyAsync("Bad word list doesn't contain " + word);
            }
        }

        [Command("addword")]
        [Alias("addbadword")]
        [HasAdmin]
        public async Task AddBadWord(string word, string euphemism = null, float size = 0.5f) {
            if (!((SocketGuildUser)Context.User).HasAdmin()) {
                await ReplyAsync("You do have administrator permission");
                return;
            }
            BadWord badWord = new BadWord {
                word = word,
                euphemism = euphemism,
                size = size
            };
            List<BadWord> badWords = Context.Guild.LoadBadWords();

            if (badWords == null) {
                badWords = new List<BadWord>();
            }
            badWords.Add(badWord);
            badWords.SaveBadWords(Context.Guild);

            if (euphemism != null) {
                await ReplyAsync("added " + badWord.word + " also known as " + euphemism + " to bad word list");
            } else {
                await ReplyAsync("added " + badWord.word + " to bad word list");
            }
        }
    }
}
