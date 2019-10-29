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
            client.ReactionAdded += HandleReaction;
            new LogMessage(LogSeverity.Info, "Filter", "Filter is active").Log();
        }

        public async Task CheckEdit(Cacheable<IMessage, ulong> oldMessage, SocketMessage editedMessage, ISocketMessageChannel channel)
            => _ = CheckMessage(editedMessage);

        public async Task HandleReaction(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction) 
            => _ = CheckReaction(cachedMessage, channel, reaction);

        public async Task CheckReaction(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction) {
            try {
                if ((reaction.User.IsSpecified && reaction.User.Value.IsBot) || !(channel is SocketGuildChannel)) {
                    return; //Makes sure it's not logging a message from a bot and that it's in a discord server
                }
                var message = cachedMessage.GetOrDownloadAsync().Result;
                SocketGuildChannel chnl = channel as SocketGuildChannel;
                SocketGuild guild = chnl?.Guild;
                if (guild == null) return; 
                
                ModerationSettings settings = guild.LoadFromFile<ModerationSettings>(false);
                SocketGuildUser gUser = guild.GetUser(reaction.UserId);
                var Guild = chnl.Guild;
                if (settings?.badUEmojis.IsNullOrEmpty() ?? true || (reaction.User.Value as SocketGuildUser).CantBeWarned() || reaction.User.Value.IsBot) {
                    return;
                }
                if (settings.badUEmojis.Select(emoji => new Emoji(emoji)).Contains(reaction.Emote)) {
                    await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                    await ((SocketGuildUser)reaction.User.Value).Warn(1, "Bad reaction used", channel as SocketTextChannel);
                    IUserMessage warnMessage = await channel.SendMessageAsync(
                        $"{reaction.User.Value.Mention} has been given their {(reaction.User.Value as SocketGuildUser).LoadInfractions().Count.Suffix()} infraction because of bad reaction used");
                }
            } catch (Exception e) {
                await new LogMessage(LogSeverity.Error, "Filter", "Something went wrong with the reaction filter", e).Log();
            }
        }

        public async Task CheckMessage(SocketMessage message) {
            try {
                if (message.Author.IsBot || !(message.Channel is SocketGuildChannel) || !(message is SocketUserMessage)) {
                    return; //Makes sure it's not logging a message from a bot and that it's in a discord server
                }
                SocketCommandContext context = new SocketCommandContext(client, message as SocketUserMessage);
                SocketGuildChannel chnl = message.Channel as SocketGuildChannel;

                if (chnl?.Guild == null) return;
                var Guild = chnl.Guild;
                ModerationSettings modSettings = Guild.LoadFromFile<ModerationSettings>();
                SocketGuildUser gUser = message.Author as SocketGuildUser;
                List<BadWord> badWords = Guild.LoadFromFile<BadWordList>()?.badWords;

                if (modSettings != null) {
                    if (modSettings.channelsWithoutAutoMod != null && modSettings.channelsWithoutAutoMod.Contains(chnl.Id) || (message.Author as SocketGuildUser).CantBeWarned())
                        return; //Returns if channel is set as not using automod

                    //Checks if a message contains an invite
                    if (!modSettings.invitesAllowed && message.Content.ToLower().Contains("discord.gg/") || message.Content.ToLower().Contains("discordapp.com/invite/")) {
                        await context.Punish("Posted Invite");
                        return;
                    }

                    //Checks for links
                    if ((modSettings.allowedLinks != null && modSettings.allowedLinks.Count > 0) && (modSettings.allowedToLink == null || !gUser.RoleIDs().Intersect(modSettings.allowedToLink).Any())) {
                        const string linkRegex = @"^((?:https?|steam):\/\/[^\s<]+[^<.,:;" + "\"\'\\]\\s])";
                        MatchCollection matches = Regex.Matches(message.Content, linkRegex, RegexOptions.IgnoreCase);
                        //if (matches != null && matches.Count > 0) await new LogMessage(LogSeverity.Info, "Filter", "Link detected").Log();
                        foreach (Match match in matches) {
                            if (!modSettings.allowedLinks.Any(s => match.ToString().ToLower().Contains(s.ToLower()))) {
                                await context.Punish("Using unauthorized links", 1);
                                return;
                            }
                        }
                    }

                    //Check for emojis
                    if (modSettings.badUEmojis.NotEmpty() && modSettings.badUEmojis.Any(s => message.Content.Contains(s))) {
                        await context.Punish("Bad emoji used", 0.8f);
                        return;
                    }

                    if (modSettings.allowedCaps > 0 && message.Content.Length > 5) {
                        uint amountCaps = 0;
                        foreach (char c in message.Content) {
                            if (char.IsUpper(c)) {
                                amountCaps++;
                            }
                        }
                        if (((amountCaps / (float)message.Content.Length) * 100) >= modSettings.allowedCaps) {
                            await context.Punish("Excessive caps", 0.3f);
                            return;
                        }
                    }
                } //End of stuff from mod settings

                //Checks for bad words
                if (badWords != null) {
                    StringBuilder sb = new StringBuilder();
                    foreach (char c in message.Content) {
                        if (sb.ToString().Length + 1 == message.Content.Length) {
                            sb.Append(c);
                            break;
                        }
                        switch (c) {
                            case '1':
                            case '!':
                                sb.Append('i');
                                break;
                            case '0':
                                sb.Append('o');
                                break;
                            case '8':
                                sb.Append('b');
                                break;
                            case '3':
                                sb.Append('e');
                                break;
                            case '$':
                                sb.Append('s');
                                break;
                            case '@':
                            case '4':
                                sb.Append('a');
                                break;
                            default:
                                if (!char.IsPunctuation(c) && !char.IsSymbol(c)) sb.Append(c);
                                break;
                        }
                    }

                    string strippedMessage = sb.ToString().ToLower();

                    foreach (BadWord badWord in badWords) {
                        if (badWord.partOfWord) {
                            if (strippedMessage.Contains(badWord.word.ToLower())) {
                                if (badWord.euphemism != null && badWord.euphemism != "") {
                                    await context.Punish("Bad word used (" + badWord.euphemism + ")");
                                } else {
                                    await context.Punish("Bad word used");
                                }

                                return;
                            }
                        } else {
                            string[] messageParts = strippedMessage.Split(' ');
                            foreach (string word in messageParts) {
                                if (word == badWord.word.ToLower()) {
                                    if (badWord.euphemism != null && badWord.euphemism != "") {
                                        await context.Punish("Bad word used (" + badWord.euphemism + ")", badWord.size);
                                    } else {
                                        await context.Punish("Bad word used", badWord.size);
                                    }
                                    return;
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                await new LogMessage(LogSeverity.Error, "Filter", "Something went wrong with the message filter", e).Log();
            }
        }
    }

    public static class FilterActions {
        public static async Task Punish(this SocketCommandContext context, string reason, float warnSize = 0.5f) {
            string jumpLink = Logging.LogMessage(reason, context.Message, context.Guild);
            await ((SocketGuildUser)context.User).Warn(warnSize, reason, context.Channel as SocketTextChannel, logLink: jumpLink);

            IUserMessage warnMessage = await context.Message.Channel.SendMessageAsync($"{context.User.Mention} has been given their {(context.User as SocketGuildUser).LoadInfractions().Count.Suffix()} infraction because of {reason}");
            try {
                await context.Message.DeleteAsync();
            } catch {
                _ = warnMessage.ModifyAsync(msg => msg.Content += ", something went wrong removing the message.");
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
                ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
                BadWords badWords = new BadWords(Context.Guild);

                var embed = new EmbedBuilder();
                embed.Author = new EmbedAuthorBuilder().WithName("Automod information for " + Context.Guild.Name + " discord");
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
                    embed.AddField("Warn for posting invite", !settings.invitesAllowed, true);
                    if (settings.allowedLinks == null || settings.allowedLinks.Count == 0) {
                        embed.AddField("Allowed links", "Links aren't moderated  ", true);
                    } else {
                        message = settings.allowedLinks.ListItems("\n");
                        if (message.NotEmpty()) embed.AddField("Allowed links", message, true);
                        if (settings.allowedToLink != null && settings.allowedToLink.Count > 0) {
                            message = Context.Guild.Roles.Where(
                                role => (role.Permissions.Administrator && !role.IsManaged) || settings.allowedToLink.Contains(role.Id)).Select(role => role.Name).ToArray().ListItems("\n");
                            if (message.NotEmpty()) embed.AddField("Roles that can post links", message, true);
                        }
                    }
                    if (settings.allowedCaps > 0) {
                        embed.AddField("Allowed caps", $"{settings.allowedCaps}% in msgs more than 5 long");
                    } else {
                        embed.AddField("Allowed caps", "Capitalization is not filtered");
                    }
                    string badUniEmojis = settings.badUEmojis?.ListItems("");
                    if (!badUniEmojis.IsNullOrEmpty()) {
                        embed.AddField("Banned Emojis", badUniEmojis, true);
                    }
                }
                if (badWords != null && badWords.all != null && badWords.all.Count > 0) {
                    List<string> words = new List<string>();
                    foreach (List<BadWord> group in badWords.grouped) {
                        BadWord first = group.FirstOrDefault();
                        if (first != null) {
                            string word = "";
                            if (useExplicit) {
                                if (group.Count == 1 || group.All(badWord => badWord.size == first.size)) {
                                    word = $"[{first.size}x] ";
                                } else {
                                    var sizes = group.Select(badword => badword.size);
                                    word = $"[{sizes.Min()}-{sizes.Max()}x] ";
                                }
                                if (first.euphemism.NotEmpty()) word += $"{first.euphemism} ";
                                word += $"({group.Select(badWord => badWord.word).ToArray().ListItems(", ")})";
                            } else if (!first.euphemism.IsNullOrEmpty()) word = first.euphemism;
                            if (first.partOfWord && (!first.euphemism.IsNullOrEmpty() || useExplicit)) {
                                word += "⌝";
                            }
                            words.Add(word);
                        } else {
                            _ = new LogMessage(LogSeverity.Error, "Filter", "Empty badword list in badwords").Log();
                        }
                    }
                    /*foreach (BadWord badWord in badWords.all) { Old code
                        string word = "";
                        if (useExplicit) {
                            word = $"[{badWord.size}x] " ;
                            if (badWord.euphemism.NotEmpty()) word += $"{badWord.euphemism} ";
                            word += $"({badWord.word})";
                        } 
                        else if (!badWord.euphemism.IsNullOrEmpty()) word = badWord.euphemism;
                        if (badWord.partOfWord && (useExplicit || !badWord.euphemism.IsNullOrEmpty())) {
                            word += "⌝";
                        }
                        words.Add(word);
                    }*/
                    message = words.ListItems("\n");
                    embed.AddField("Badword euphemisms (not an exhaustive list)", message, false);
                }
                IDMChannel channel = Context.Message.Author.GetOrCreateDMChannelAsync().Result;
                if (channel != null) {
                    _ = channel.SendMessageAsync("The symbol '⌝' next to a word means that you can be warned for a word that contains the bad word", embed: embed.Build());
                } else {
                    _ = ReplyAsync(Context.Message.Author.Mention + " we can't send a message to your DMs");
                }
            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "Settings", "Error", e).Log();
            }
        }

        [HasAdmin]
        [Command("maxemoji"), Alias("setmaxemoji")]
        public async Task AllowEmojis(uint amount) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (amount == settings.maxEmojis) {
                await ReplyAsync("The selected value is already set");
                return;
            }
            settings.maxEmojis = amount;
            settings.SaveToFile(Context.Guild);
            string extraInfo = "";
            if (settings.allowedToLink.NotEmpty()) extraInfo = " except by role allowed to link";
            if (amount == 0) await ReplyAsync("No emojis are allowed" + extraInfo);
            else await ReplyAsync($"Max {amount} emojis are allowed{extraInfo}");
        }

        [HasAdmin]
        [Command("allowemoji"), Alias("setmaxemojis")]
        public async Task SetMaxEmojis(string amount) {
            ModerationSettings settings = null;
            switch (amount.ToLower()) {
                case "null":
                case "none":
                case "disable":
                    settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
                    if (settings?.maxEmojis == null)
                        await ReplyAsync("Emoji moderation is already disabled");
                    else {
                        settings.maxEmojis = null;
                        settings.SaveToFile(Context.Guild);
                        await ReplyAsync("Emoji moderation is now disabled");
                    }
                    break;
                case "all":
                    settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
                    settings.maxEmojis = 0;
                    settings.SaveToFile(Context.Guild);
                    string extraInfo = "";
                    if (settings.allowedToLink.NotEmpty()) extraInfo = " except by role allowed to link";
                    await ReplyAsync("Emojis are now no longer allowed" + extraInfo);
                    break;
                default:
                    await ReplyAsync("Input not understood");
                    break;
            }
        }

        [HasAdmin]
        [Command("banemoji"), Alias("disallowemoji")]
        [RequireBotPermission(ChannelPermission.AddReactions)]
        public async Task BanEmoji(Emoji emoji) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.badUEmojis.Contains(emoji.Name)) {
                await ReplyAsync($"Emoji {emoji.Name} is already banned");
                return;
            }
            settings.badUEmojis.Add(emoji.Name);
            settings.SaveToFile(Context.Guild);
            await ReplyAsync($"Emoji {emoji.Name} is now banned");
        }

        [HasAdmin]
        [Command("allowemoji"), Alias("unbanemoji")]
        [RequireBotPermission(ChannelPermission.AddReactions)]
        public async Task RemoveBannedEmoji(Emoji emoji) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
            if (settings == null || !settings.badUEmojis.Contains(emoji.Name)) {
                await ReplyAsync($"Emoji {emoji.Name} is not banned");
                return;
            }
            settings.badUEmojis.Remove(emoji.Name);
            settings.SaveToFile(Context.Guild);
            await ReplyAsync($"Emoji {emoji.Name} is now not banned");
        }

        [HasAdmin]
        [Command("SetAllowedCaps")]
        public async Task SetCapFilter(ushort percent) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (percent == settings.allowedCaps) {
                await ReplyAsync("That's the current setting");
                return;
            }
            if (percent > 100) {
                await ReplyAsync("How do you expect that to work genius?");
                return;
            }
            settings.allowedCaps = percent;
            settings.SaveToFile(Context.Guild);

            if (percent == 0) {
                await ReplyAsync("Disabled capitalization filtering");
                return;
            }
            await ReplyAsync($"Set messages to filtered if they are longer than 5 characters and {percent}% of letters are capitalized");
        }

        [HasAdmin()]
        [Command("AddAllowedLinkRole")]
        [Alias("addroleallowedtolink")]
        public async Task AddAllowedLinkRole(SocketRole role) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (settings.allowedToLink == null) settings.allowedToLink = new List<ulong>();
            if (settings.allowedToLink.Contains(role.Id)) {
                await ReplyAsync($"Role '{role.Name}' was already exempt from link filtering");
                return;
            }
            settings.allowedToLink.Add(role.Id);
            settings.SaveToFile(Context.Guild);

            await ReplyAsync($"Role '{role.Name}' is now exempt from link filtering");
        }

        [HasAdmin()]
        [Command("RemoveAllowedLinkRole")]
        [Alias("removeroleallowedtolink")]
        public async Task RemoveAllowedLinkRole(SocketRole role) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();

            if (settings == null || settings.allowedToLink == null) {
                await ReplyAsync($"No role is currently excused from posting links");
                return;
            }
            if (settings.allowedToLink.Contains(role.Id)) {
                settings.allowedToLink.Remove(role.Id);
                settings.SaveToFile(Context.Guild);
                await ReplyAsync($"Role '{role.Name}' is no longer exempt from link filtering");
                return;
            }

            await ReplyAsync($"Role '{role.Name}' wasn't exempt from link filtering");
        }

        [Command("ToggleContainBadWord")]
        [Alias("togglecontainword", "togglecontainword")]
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
                    BadWordList badWordList = new BadWordList { badWords = badWords.all };
                    badWordList.SaveToFile(Context.Guild);
                    return;
                }
            }
            await ReplyAsync("Badword not found");
        }

        [Command("channeltoggle")]
        [HasAdmin]
        public async Task ToggleAutoMod() {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (settings.channelsWithoutAutoMod.Contains(Context.Channel.Id)) {
                settings.channelsWithoutAutoMod.Remove(Context.Channel.Id);
                await ReplyAsync("Enabled automod in this channel");
            } else {
                settings.channelsWithoutAutoMod.Add(Context.Channel.Id);
                await ReplyAsync("Disabled automod in this channel");
            }

            settings.SaveToFile(Context.Guild);
        }

        [Command("addignoredrole")]
        [HasAdmin]
        public async Task AddWarnIgnoredRole(SocketRole role) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.cantBeWarned == null) settings.cantBeWarned = new List<ulong>();
            else if (settings.cantBeWarned.Contains(role.Id)) {
                await ReplyAsync($"Role '{role.Name}' is already not able to be warned");
                return;
            }
            settings.cantBeWarned.Add(role.Id);
            settings.SaveToFile(Context.Guild);
            await ReplyAsync($"Role '{role.Name}' will not be able to be warned now");
        }

        [Command("removeignoredrole")]
        [HasAdmin]
        public async Task RemovedWarnIgnoredRole(SocketRole role) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (settings == null || settings.cantBeWarned == null) settings.cantBeWarned = new List<ulong>();
            else if (settings.cantBeWarned.Contains(role.Id)) {
                await ReplyAsync($"Role '{role.Name}' is already able to be warned");
            } else {
                settings.cantBeWarned.Add(role.Id);
                settings.SaveToFile(Context.Guild);
                await ReplyAsync($"Role '{role.Name}' will not be able to be warned now");
            }
        }

        [Command("addallowedlink")]
        [HasAdmin]
        public async Task AddAllowedLink(string link) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.allowedLinks == null) settings.allowedLinks = new List<string>();
            settings.allowedLinks.Add(link);
            settings.SaveToFile(Context.Guild);
            await ReplyAsync("People will now be allowed to use " + link);
        }

        [Command("removeallowedlink")]
        [HasAdmin]
        public async Task RemoveAllowedLink(string link) {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.allowedLinks == null || !settings.allowedLinks.Contains(link)) {
                await ReplyAsync("Link is already not allowed");
                return;
            }
            settings.allowedLinks.Remove(link);
            if (settings.allowedLinks.Count == 0) settings.allowedLinks = null;
            settings.SaveToFile(Context.Guild);
            await ReplyAsync("People will no longer be allowed to use " + link);
        }

        [Command("toggleinvitewarn")]
        [HasAdmin]
        public async Task ToggleInviteWarn() {
            IUserMessage message = await ReplyAsync("Trying to toggle");
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (settings == null) {
                settings = new ModerationSettings();
                Console.WriteLine(DateTime.Now.ToShortTimeString() + " Creating new mod settings");
            }
            settings.invitesAllowed = !settings.invitesAllowed;
            settings.SaveToFile(Context.Guild);

            await message.ModifyAsync(msg => msg.Content = "set invites allowed to " + settings.invitesAllowed.ToString().ToLower());
        }

        [Command("removeword")]
        [Alias("removebadword")]
        [HasAdmin]
        public async Task RemoveBadWord(string word) {
            BadWordList badWordsClass = Context.Guild.LoadFromFile<BadWordList>(true);

            if (badWordsClass == null) {
                await ReplyAsync("Bad words is null");
                return;
            }
            BadWord badToRemove = null;
            foreach (BadWord badWord in badWordsClass.badWords) {
                if (badWord.word == word) {
                    badToRemove = badWord;
                }
            }
            if (badToRemove != null) {
                badWordsClass.badWords.Remove(badToRemove);
                badWordsClass.SaveToFile(Context.Guild);

                await ReplyAsync("removed " + word + " from bad word list");
            } else {
                await ReplyAsync("Bad word list doesn't contain " + word);
            }
        }

        [Command("addword")]
        [Alias("addbadword")]
        [HasAdmin]
        public async Task AddBadWord(string word, string euphemism = null, float size = 0.5f) {
            BadWord badWord = new BadWord {
                word = word,
                euphemism = euphemism,
                size = size
            };
            //List<BadWord> badWords = Context.Guild.LoadFromFile<BadWordList>().badWords ?? new List<BadWord>();
            BadWordList badWordsClass = Context.Guild.LoadFromFile<BadWordList>(true);
            badWordsClass.badWords.Add(badWord);
            badWordsClass.SaveToFile(Context.Guild);

            if (euphemism != null) {
                await ReplyAsync("added " + badWord.word + " also known as " + euphemism + " to bad word list");
            } else {
                await ReplyAsync("added " + badWord.word + " to bad word list");
            }
        }
    }
}
