using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCatMaxy.Moderation;
using Discord.WebSocket;
using Discord.Commands;
using BotCatMaxy.Data;
using Newtonsoft.Json;
using Discord.Rest;
using System.Text;
using System.Linq;
using BotCatMaxy;
using Discord;
using System;
using Discord.Addons.Interactive;
using System.Dynamic;
using System.Runtime.InteropServices.ComTypes;

namespace BotCatMaxy {
    public class Filter {
        readonly DiscordSocketClient client;
        public Filter(DiscordSocketClient client) {
            this.client = client;
            client.MessageReceived += CheckMessage;
            client.MessageUpdated += HandleEdit;
            client.ReactionAdded += HandleReaction;
            client.UserJoined += HandleUserJoin;
            new LogMessage(LogSeverity.Info, "Filter", "Filter is active").Log();
        }

        public async Task HandleEdit(Cacheable<IMessage, ulong> oldMessage, SocketMessage editedMessage, ISocketMessageChannel channel)
            => await Task.Run(() => CheckMessage(editedMessage)).ConfigureAwait(false);

        public async Task HandleReaction(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction)
            => await Task.Run(() => CheckReaction(cachedMessage, channel, reaction)).ConfigureAwait(false);

        public async Task HandleUserJoin(SocketGuildUser user)
            => await Task.Run(async () => CheckNameInGuild(user, user.Guild));

        public async Task HandleMessage(SocketMessage message)
            => await Task.Run(() => CheckMessage(message)).ConfigureAwait(false);

        public async Task CheckNameInGuild(IUser user, SocketGuild guild) {
            try {
                ModerationSettings settings = guild.LoadFromFile<ModerationSettings>(false);
                //Has to check if not equal to true since it's nullable
                if (settings?.moderateNames != true) return;
                BadWord detectedBadWord = user.Username.CheckForBadWords(guild.LoadFromFile<BadWordList>(false)?.badWords.ToArray());
                if (detectedBadWord == null) return;

                LogSettings logSettings = guild.LoadFromFile<LogSettings>(false);
                if (logSettings?.logChannel != null && guild.TryGetChannel(logSettings.logChannel ?? 0, out IGuildChannel channel)) {
                    EmbedBuilder embed = new EmbedBuilder();
                    embed.WithColor(Color.DarkMagenta);
                    embed.WithAuthor(user);
                    embed.WithTitle("User kicked for bad username");
                    embed.WithDescription($"Name '{user.Username}' contained '{detectedBadWord.word}'");
                    embed.WithCurrentTimestamp();
                    embed.WithFooter("User ID: " + user.Id);
                    await (channel as SocketTextChannel).SendMessageAsync(embed: embed.Build());
                }
                //If user's DMs aren't blocked
                if (user.TryNotify($"Your username contains a filtered word ({detectedBadWord.word}). Please change it before rejoining {guild.Name} Discord")) {
                    SocketGuildUser gUser = user as SocketGuildUser ?? guild.GetUser(user.Id);
                    await gUser.KickAsync($"Username '{user.Username}' triggered autofilter for '{detectedBadWord.word}'");
                    user.Id.AddWarn(1, "Username with filtered word", guild, null);
                    return;
                }//If user's DMs are blocked
                if (logSettings != null) {
                    if (guild.TryGetTextChannel(logSettings.backupChannel, out ITextChannel backupChannel))
                        await backupChannel.SendMessageAsync($"{user.Mention} your username contains a bad word but your DMs are closed. Please clean up your username before rejoining");
                    else if (guild.TryGetTextChannel(logSettings.pubLogChannel, out ITextChannel pubChannel))
                        await pubChannel.SendMessageAsync($"{user.Mention} your username contains a bad word but your DMs are closed. Please clean up your username before rejoining");
                    await Task.Delay(10000);
                    SocketGuildUser gUser = user as SocketGuildUser ?? guild.GetUser(user.Id);
                    await gUser.KickAsync($"Username '{user.Username}' triggered autofilter for '{detectedBadWord.word}'");
                    user.Id.AddWarn(1, "Username with filtered word (Note: DMs closed)", guild, null);

                }
            } catch (Exception e) {
                await new LogMessage(LogSeverity.Error, "Filter", "Something went wrong checking usernames", e).Log();
            }
        }

        public async Task CheckReaction(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction) {
            try {
                if ((reaction.User.IsSpecified && reaction.User.Value.IsBot) || !(channel is SocketGuildChannel)) {
                    return; //Makes sure it's not logging a message from a bot and that it's in a discord server
                }
                IUserMessage message = await cachedMessage.GetOrDownloadAsync();
                SocketGuildChannel chnl = channel as SocketGuildChannel;
                SocketGuild guild = chnl?.Guild;
                if (guild == null) return;

                ModerationSettings settings = guild.LoadFromFile<ModerationSettings>(false);
                SocketGuildUser gUser = guild.GetUser(reaction.UserId);
                var Guild = chnl.Guild;
                if (settings?.badUEmojis?.Count == null || settings.badUEmojis.Count == 0 || (reaction.User.Value as SocketGuildUser).CantBeWarned() || reaction.User.Value.IsBot) {
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

                if (chnl?.Guild == null || (message.Author as SocketGuildUser).CantBeWarned()) return;
                var Guild = chnl.Guild;
                ModerationSettings modSettings = Guild.LoadFromFile<ModerationSettings>();
                SocketGuildUser gUser = message.Author as SocketGuildUser;
                List<BadWord> badWords = Guild.LoadFromFile<BadWordList>()?.badWords;

                if (modSettings != null) {
                    if (modSettings.channelsWithoutAutoMod != null && modSettings.channelsWithoutAutoMod.Contains(chnl.Id))
                        return; //Returns if channel is set as not using automod

                    //Checks if a message contains an invite
                    if (!modSettings.invitesAllowed && message.Content.ToLower().Contains("discord.gg/") || message.Content.ToLower().Contains("discordapp.com/invite/")) {
                        await context.FilterPunish("Posted Invite", modSettings);
                        return;
                    }

                    //Checks for links
                    if ((modSettings.allowedLinks != null && modSettings.allowedLinks.Count > 0) && (modSettings.allowedToLink == null || !gUser.RoleIDs().Intersect(modSettings.allowedToLink).Any())) {
                        const string linkRegex = @"((?:https?|steam):\/\/[^\s<]+[^<.,:;" + "\"\'\\]\\s])";
                        MatchCollection matches = Regex.Matches(message.Content, linkRegex, RegexOptions.IgnoreCase);
                        //if (matches != null && matches.Count > 0) await new LogMessage(LogSeverity.Info, "Filter", "Link detected").Log();
                        foreach (Match match in matches) {
                            if (!modSettings.allowedLinks.Any(s => match.ToString().ToLower().Contains(s.ToLower()))) {
                                await context.FilterPunish("Using unauthorized links", modSettings, 1);
                                return;
                            }
                        }
                    }

                    //Check for emojis
                    if (modSettings.badUEmojis.NotEmpty() && modSettings.badUEmojis.Any(s => message.Content.Contains(s))) {
                        await context.FilterPunish("Bad emoji used", modSettings, 0.8f);
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
                            await context.FilterPunish("Excessive caps", modSettings, 0.3f);
                            return;
                        }
                    }
                } //End of stuff from mod settings

                BadWord detectedBadWord = message.Content.CheckForBadWords(badWords.ToArray());
                if (detectedBadWord != null) {
                    if (!string.IsNullOrEmpty(detectedBadWord.euphemism))
                        await context.FilterPunish("Bad word used (" + detectedBadWord.euphemism + ")", modSettings, detectedBadWord.size);
                    else
                        await context.FilterPunish("Bad word used", modSettings, detectedBadWord.size);
                }

            } catch (Exception e) {
                await new LogMessage(LogSeverity.Error, "Filter", "Something went wrong with the message filter", e).Log();
            }
        }

    }

    public static class FilterFunctions {
        readonly static char[] splitters = @"#.,;/\|=_- ".ToCharArray();

        public static BadWord CheckForBadWords(this string message, BadWord[] badWords) {
            if (badWords.IsNullOrEmpty()) return null;

            //Checks for bad words
            StringBuilder sb = new StringBuilder();
            foreach (char c in message) {
                switch (c) {
                    case '@':
                    case '4':
                        sb.Append('a');
                        break;
                    case '8':
                        sb.Append('b');
                        break;
                    case '¢':
                        sb.Append('c');
                        break;
                    case '3':
                        sb.Append('e');
                        break;
                    case '!':
                        sb.Append('i');
                        break;
                    case '0':
                        sb.Append('o');
                        break;
                    case '$':
                        sb.Append('s');
                        break;
                    default:
                        if (!char.IsPunctuation(c) && !char.IsSymbol(c)) sb.Append(c);
                        break;
                }
            }

            string strippedMessage = sb.ToString();
            //splits string into words separated by space, '-' or '_'
            string[] messageParts = message.Split(splitters, StringSplitOptions.RemoveEmptyEntries);

            foreach (BadWord badWord in badWords) {
                if (badWord.partOfWord) {
                    if (strippedMessage.Contains(badWord.word, StringComparison.InvariantCultureIgnoreCase)) {
                        return badWord;
                    }
                } else { //If bad word is ignored inside of words
                    foreach (string word in messageParts) {
                        if (word.Equals(badWord.word, StringComparison.InvariantCultureIgnoreCase)) {
                            return badWord;
                        }
                    }
                }
            }
            return null;
        }

    }

    [Group("automod")]
    [Alias("auto-mod", "filter")]
    public class FilterSettingCommands : InteractiveBase<SocketCommandContext> {
        [Command("list")]
        [Alias("info")]
        [RequireContext(ContextType.DM, ErrorMessage = "This command now only works in the bot's DMs")]
        public async Task ListAutoMod(string extension = "") {
            var mutualGuilds = Context.Message.Author.MutualGuilds.ToArray();

            var guildsEmbed = new EmbedBuilder();
            guildsEmbed.WithTitle("Reply with the the number next to the guild you want to check the filter info from");

            for (int i = 0; i < mutualGuilds.Length; i++) {
                guildsEmbed.AddField($"[{i + 1}] {mutualGuilds[i].Name} discord", mutualGuilds[i].Id);
            }
            await ReplyAsync(embed: guildsEmbed.Build());
            SocketGuild guild;
            while (true) {
                SocketMessage reply = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
                if (reply == null || reply.Content == "cancel") {
                    await ReplyAsync("You have timed out or canceled");
                    return;
                }
                try {
                    guild = mutualGuilds[ushort.Parse(reply.Content) - 1];
                    break;
                } catch {
                    await ReplyAsync("Invalid number, please reply again with a valid number or ``cancel``");
                }
            }

            ModerationSettings settings = guild.LoadFromFile<ModerationSettings>(false);
            BadWords badWords = new BadWords(guild);

            var embed = new EmbedBuilder();
            embed.Author = new EmbedAuthorBuilder().WithName("Automod information for " + guild.Name + " discord");
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
                        message = guild.Roles.Where(
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
                            word += $"({group.Select(badWord => $"{badWord.word}{(badWord.partOfWord ? "¤" : "")}").ToArray().ListItems(", ")})";
                        } else if (!first.euphemism.IsNullOrEmpty())
                            word = first.euphemism;
                        if (first.partOfWord && (!first.euphemism.IsNullOrEmpty() && !useExplicit)) {
                            word += "¤";
                        }
                        words.Add(word);
                    } else {
                        _ = new LogMessage(LogSeverity.Error, "Filter", "Empty badword list in badwords").Log();
                    }
                }
                message = words.ListItems("\n");
                embed.AddField("Badword euphemisms (not an exhaustive list)", message, false);
            }

            await ReplyAsync("The symbol '¤' next to a word means that you can be warned for a word that contains the bad word", embed: embed.Build());
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
            settings.SaveToFile();
            string extraInfo = "";
            if (settings.allowedToLink.NotEmpty()) extraInfo = " except by role allowed to link";
            if (amount == 0) await ReplyAsync("No emojis are allowed" + extraInfo);
            else await ReplyAsync($"Max {amount} emojis are allowed{extraInfo}");
        }

        [HasAdmin]
        [Command("allowemoji"), Alias("setmaxemojis")]
        public async Task SetMaxEmojis(string amount) {
            ModerationSettings settings;
            switch (amount.ToLower()) {
                case "null":
                case "none":
                case "disable":
                    settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
                    if (settings?.maxEmojis == null)
                        await ReplyAsync("Emoji moderation is already disabled");
                    else {
                        settings.maxEmojis = null;
                        settings.SaveToFile();
                        await ReplyAsync("Emoji moderation is now disabled");
                    }
                    break;
                case "all":
                    settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
                    settings.maxEmojis = 0;
                    settings.SaveToFile();
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
            settings.SaveToFile();
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
            settings.SaveToFile();
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
            settings.SaveToFile();

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
            settings.SaveToFile();

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
                settings.SaveToFile();
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
                    BadWordList badWordList = new BadWordList { badWords = badWords.all, guild = Context.Guild };
                    badWordList.SaveToFile();
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

            settings.SaveToFile();
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
            settings.SaveToFile();
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
                settings.SaveToFile();
                await ReplyAsync($"Role '{role.Name}' will not be able to be warned now");
            }
        }

        public const string validLinkRegex = @"^[\w\d]+\.[\w\d]+$";

        [Command("addallowedlink")]
        [HasAdmin]
        public async Task AddAllowedLink(string link) {
            if (!Regex.IsMatch(link, validLinkRegex)) {
                await ReplyAsync("Link is not valid");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.allowedLinks == null) settings.allowedLinks = new HashSet<string>();
            else if (settings.allowedLinks.Contains(link)) {
                await ReplyAsync("Link is already in whitelist");
                return;
            }
            settings.allowedLinks.Add(link);
            settings.SaveToFile();
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
            settings.SaveToFile();
            await ReplyAsync("People will no longer be allowed to use " + link);
        }

        [Command("toggleinvitewarn")]
        [HasAdmin]
        public async Task ToggleInviteWarn() {
            IUserMessage message = await ReplyAsync("Trying to toggle");
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            settings.invitesAllowed = !settings.invitesAllowed;
            settings.SaveToFile();

            await message.ModifyAsync(msg => msg.Content = "set invites allowed to " + settings.invitesAllowed.ToString().ToLower());
        }

        [Command("removeword")]
        [Alias("removebadword", "badremove", "removebadword")]
        [HasAdmin]
        public async Task RemoveBadWord(string word) {
            BadWordList badWordsClass = Context.Guild.LoadFromFile<BadWordList>(false);

            if (badWordsClass == null) {
                await ReplyAsync("No bad words are set");
                return;
            }
            BadWord badToRemove = badWordsClass.badWords.FirstOrDefault(badWord => badWord.word == word);
            if (badToRemove != null) {
                badWordsClass.badWords.Remove(badToRemove);
                badWordsClass.SaveToFile();

                await ReplyAsync($"Removed {word} from bad word list");
            } else {
                await ReplyAsync("Bad word list doesn't contain " + word);
            }
        }

        [Command("addword")]
        [Alias("addbadword", "wordadd", "badwordadd")]
        [HasAdmin]
        public async Task AddBadWord(string word, string euphemism = null, float size = 0.5f) {
            BadWord badWord = new BadWord {
                word = word,
                euphemism = euphemism,
                size = size
            };
            BadWordList badWordsClass = Context.Guild.LoadFromFile<BadWordList>(true);
            badWordsClass.badWords.Add(badWord);
            badWordsClass.SaveToFile();

            await ReplyAsync($"Added {badWord.word}{((badWord.euphemism != null) ? $", also known as {badWord.euphemism}" : "")} to bad word list");
        }

        [Command("addanouncementchannel"), HasAdmin]
        public async Task AddAnouncementChannel() {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.anouncementChannels.Contains(Context.Channel.Id)) {
                await ReplyAsync("This is already an 'anouncement' channel");
                return;
            }
            settings.anouncementChannels.Add(Context.Channel.Id);
            settings.SaveToFile();
            await ReplyAsync("This channel is now an 'anouncement' channel");
        }

        [Command("removeanouncementchannel"), HasAdmin]
        public async Task RemoveAnouncementChannel() {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
            if (!settings?.anouncementChannels?.Contains(Context.Channel.Id) ?? true) {
                //Goes through various steps to check if 1. settings (for anouncement channels) exist 2. Current channel is in those settings
                await ReplyAsync("This already not an 'anouncement' channel");
                return;
            }
            settings.anouncementChannels.Remove(Context.Channel.Id);
            settings.SaveToFile();
            await ReplyAsync("This channel is now not an 'anouncement' channel");
        }

        [Command("togglenamefilter")]
        [HasAdmin]
        public async Task ToggleNameFilter() {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            settings.moderateNames = !settings.moderateNames;
            settings.SaveToFile();

            await ReplyAsync("Set new user name filtering to " + settings.moderateNames.ToString().ToLowerInvariant());
        }
    }
}
