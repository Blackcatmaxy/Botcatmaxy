using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BotCatMaxy.Components.Filter
{
    [Group("automod")]
    [Summary("Manages the automoderator.")]
    [Alias("auto-mod", "filter")]
    public class FilterCommands : InteractiveBase<SocketCommandContext>
    {
        [Command("list")]
        [Summary("View filter information.")]
        [Alias("info")]
        [RequireContext(ContextType.DM, ErrorMessage = "This command now only works in the bot's DMs")]
        public async Task ListAutoMod(string extension = "")
        {
            var mutualGuilds = Context.Message.Author.MutualGuilds.ToArray();

            var guildsEmbed = new EmbedBuilder();
            guildsEmbed.WithTitle("Reply with the the number next to the guild you want to check the filter info from");

            for (int i = 0; i < mutualGuilds.Length; i++)
            {
                guildsEmbed.AddField($"[{i + 1}] {mutualGuilds[i].Name} discord", mutualGuilds[i].Id);
            }
            await ReplyAsync(embed: guildsEmbed.Build());
            SocketGuild guild;
            while (true)
            {
                SocketMessage reply = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
                if (reply == null || reply.Content == "cancel")
                {
                    await ReplyAsync("You have timed out or canceled");
                    return;
                }
                try
                {
                    guild = mutualGuilds[ushort.Parse(reply.Content) - 1];
                    break;
                }
                catch
                {
                    await ReplyAsync("Invalid number, please reply again with a valid number or ``cancel``");
                }
            }

            ModerationSettings settings = guild.LoadFromFile<ModerationSettings>(false);
            BadWords badWords = new BadWords(guild);

            var embed = new EmbedBuilder();
            embed.Author = new EmbedAuthorBuilder().WithName("Automod information for " + guild.Name + " discord");
            string message = "";

            bool useExplicit = false;
            if (extension != null && extension.ToLower() == "explicit" || extension.ToLower() == "e")
            {
                if (guild.GetUser(Context.Message.Author.Id).CanWarn())
                {
                    useExplicit = true;
                }
                else
                {
                    await ReplyAsync("You lack the permissions for viewing explicit bad words");
                    return;
                }
            }

            if (settings == null)
            {
                embed.AddField("Moderation settings", "Are null", true);
            }
            else
            {
                embed.AddField("Warn for posting invite", !settings.invitesAllowed, true);
                if (settings.allowedLinks == null || settings.allowedLinks.Count == 0)
                {
                    embed.AddField("Allowed links", "Links aren't moderated  ", true);
                }
                else
                {
                    message = settings.allowedLinks.ListItems("\n");
                    if (message.NotEmpty()) embed.AddField("Allowed links", message, true);
                    if (settings.allowedToLink != null && settings.allowedToLink.Count > 0)
                    {
                        message = guild.Roles.Where(
                            role => (role.Permissions.Administrator && !role.IsManaged) || settings.allowedToLink.Contains(role.Id)).Select(role => role.Name).ToArray().ListItems("\n");
                        if (message.NotEmpty()) embed.AddField("Roles that can post links", message, true);
                    }
                }
                if (settings.allowedCaps > 0)
                {
                    embed.AddField("Allowed caps", $"{settings.allowedCaps}% in msgs more than 5 long", true);
                }
                else
                {
                    embed.AddField("Allowed caps", "Capitalization is not filtered", true);
                }
                string badUniEmojis = settings.badUEmojis?.ListItems("");
                if (!badUniEmojis.IsNullOrEmpty())
                {
                    embed.AddField("Banned Emojis", badUniEmojis, true);
                }
                if (settings.moderateNames) embed.AddField("Name moderation", "True", true);
                if (settings.maxNewLines != null) embed.AddField("Maximum new lines", $"{settings.maxNewLines.Value} new lines", true);
            }
            if (badWords != null && badWords.all != null && badWords.all.Count > 0)
            {
                List<string> words = new List<string>();
                foreach (List<BadWord> group in badWords.grouped)
                {
                    BadWord first = group.FirstOrDefault();
                    if (first != null)
                    {
                        string word = "";
                        if (useExplicit)
                        {
                            if (group.Count == 1 || group.All(badWord => badWord.size == first.size))
                            {
                                word = $"[{first.size}x] ";
                            }
                            else
                            {
                                var sizes = group.Select(badword => badword.size);
                                word = $"[{sizes.Min()}-{sizes.Max()}x] ";
                            }
                            if (first.euphemism.NotEmpty()) word += $"{first.euphemism} ";
                            word += $"({group.Select(badWord => $"{badWord.word}{(badWord.partOfWord ? "¤" : "")}").ToArray().ListItems(", ")})";
                        }
                        else if (!first.euphemism.IsNullOrEmpty())
                            word = first.euphemism;
                        if (first.partOfWord && (!first.euphemism.IsNullOrEmpty() && !useExplicit))
                        {
                            word += "¤";
                        }
                        words.Add(word);
                    }
                    else
                    {
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
        [Summary("Set a number of max emojis a user may send in a single message.")]
        public async Task AllowEmojis(uint amount)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (amount == settings.maxEmojis)
            {
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
        [Summary("Set a number of max emojis a user may send in a single message.")]
        public async Task SetMaxEmojis(string amount)
        {
            ModerationSettings settings;
            switch (amount.ToLower())
            {
                case "null":
                case "none":
                case "disable":
                    settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
                    if (settings?.maxEmojis == null)
                        await ReplyAsync("Emoji moderation is already disabled");
                    else
                    {
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
        [Summary("Disallow users from sending a specific emoji.")]
        [RequireBotPermission(ChannelPermission.AddReactions)]
        public async Task BanEmoji(Emoji emoji)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.badUEmojis.Contains(emoji.Name))
            {
                await ReplyAsync($"Emoji {emoji.Name} is already banned");
                return;
            }
            settings.badUEmojis.Add(emoji.Name);
            settings.SaveToFile();
            await ReplyAsync($"Emoji {emoji.Name} is now banned");
        }

        [HasAdmin]
        [Command("allowemoji"), Alias("unbanemoji")]
        [Summary("Allow users from sending a specific emoji.")]
        [RequireBotPermission(ChannelPermission.AddReactions)]
        public async Task RemoveBannedEmoji(Emoji emoji)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
            if (settings == null || !settings.badUEmojis.Contains(emoji.Name))
            {
                await ReplyAsync($"Emoji {emoji.Name} is not banned");
                return;
            }
            settings.badUEmojis.Remove(emoji.Name);
            settings.SaveToFile();
            await ReplyAsync($"Emoji {emoji.Name} is now not banned");
        }

        [HasAdmin]
        [Command("SetAllowedCaps")]
        [Summary("Sets the maximum percentage of capital letters a user may send.")]
        public async Task SetCapFilter(ushort percent)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (percent == settings.allowedCaps)
            {
                await ReplyAsync("That's the current setting");
                return;
            }
            if (percent > 100)
            {
                await ReplyAsync("How do you expect that to work genius?");
                return;
            }
            settings.allowedCaps = percent;
            settings.SaveToFile();

            if (percent == 0)
            {
                await ReplyAsync("Disabled capitalization filtering");
                return;
            }
            await ReplyAsync($"Set messages to filtered if they are longer than 5 characters and {percent}% of letters are capitalized");
        }

        [HasAdmin()]
        [Command("AddAllowedLinkRole")]
        [Summary("Allows users in a role to send links.")]
        [Alias("addroleallowedtolink")]
        public async Task AddAllowedLinkRole(SocketRole role)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (settings.allowedToLink == null) settings.allowedToLink = new List<ulong>();
            if (settings.allowedToLink.Contains(role.Id))
            {
                await ReplyAsync($"Role '{role.Name}' was already exempt from link filtering");
                return;
            }
            settings.allowedToLink.Add(role.Id);
            settings.SaveToFile();

            await ReplyAsync($"Role '{role.Name}' is now exempt from link filtering");
        }

        [HasAdmin()]
        [Command("RemoveAllowedLinkRole")]
        [Summary("Disallows users in a role to send links.")]
        [Alias("removeroleallowedtolink")]
        public async Task RemoveAllowedLinkRole(SocketRole role)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();

            if (settings == null || settings.allowedToLink == null)
            {
                await ReplyAsync($"No role is currently excused from posting links");
                return;
            }
            if (settings.allowedToLink.Contains(role.Id))
            {
                settings.allowedToLink.Remove(role.Id);
                settings.SaveToFile();
                await ReplyAsync($"Role '{role.Name}' is no longer exempt from link filtering");
                return;
            }

            await ReplyAsync($"Role '{role.Name}' wasn't exempt from link filtering");
        }

        [Command("ToggleContainBadWord")]
        [Summary("Toggle strict filtering and checks if a bad word is sent even if inside another word.")]
        [Alias("togglecontainword", "togglecontainword")]
        [HasAdmin()]
        public async Task ToggleContainBadWord(string word)
        {
            BadWords badWords = new BadWords(Context.Guild);
            foreach (BadWord badWord in badWords.all)
            {
                if (badWord.word.ToLower() == word.ToLower())
                {
                    if (badWord.partOfWord)
                    {
                        badWord.partOfWord = false;
                        await ReplyAsync("Set badword to not be filtered if it's inside of another word");
                    }
                    else
                    {
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
        [Summary("Toggles if this channel is exempt from automoderation.")]
        [HasAdmin]
        public async Task ToggleAutoMod()
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (settings.channelsWithoutAutoMod.Contains(Context.Channel.Id))
            {
                settings.channelsWithoutAutoMod.Remove(Context.Channel.Id);
                await ReplyAsync("Enabled automod in this channel");
            }
            else
            {
                settings.channelsWithoutAutoMod.Add(Context.Channel.Id);
                await ReplyAsync("Disabled automod in this channel");
            }

            settings.SaveToFile();
        }

        [Command("addignoredrole")]
        [Summary("Disallows a user from being warned by the automoderator.")]
        [HasAdmin]
        public async Task AddWarnIgnoredRole(SocketRole role)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.cantBeWarned == null) settings.cantBeWarned = new List<ulong>();
            else if (settings.cantBeWarned.Contains(role.Id))
            {
                await ReplyAsync($"Role '{role.Name}' is already not able to be warned");
                return;
            }
            settings.cantBeWarned.Add(role.Id);
            settings.SaveToFile();
            await ReplyAsync($"Role '{role.Name}' will not be able to be warned now");
        }

        [Command("removeignoredrole")]
        [Summary("Allows a user from being warned by the automoderator.")]
        [HasAdmin]
        public async Task RemovedWarnIgnoredRole(SocketRole role)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (settings == null || settings.cantBeWarned == null) settings.cantBeWarned = new List<ulong>();
            else if (settings.cantBeWarned.Contains(role.Id))
            {
                await ReplyAsync($"Role '{role.Name}' is already able to be warned");
            }
            else
            {
                settings.cantBeWarned.Add(role.Id);
                settings.SaveToFile();
                await ReplyAsync($"Role '{role.Name}' will not be able to be warned now");
            }
        }

        public const string validLinkRegex = @"^[\w\d]+\.[\w\d]+$";

        [Command("addallowedlink")]
        [Summary("Allow a link to bypass the automoderator.")]
        [HasAdmin]
        public async Task AddAllowedLink(string link)
        {
            if (!Regex.IsMatch(link, validLinkRegex))
            {
                await ReplyAsync("Link is not valid");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.allowedLinks == null) settings.allowedLinks = new HashSet<string>();
            else if (settings.allowedLinks.Contains(link))
            {
                await ReplyAsync("Link is already in whitelist");
                return;
            }
            settings.allowedLinks.Add(link);
            settings.SaveToFile();
            await ReplyAsync("People will now be allowed to use " + link);
        }

        [Command("removeallowedlink")]
        [Summary("Disallow a link to bypass the automoderator.")]
        [HasAdmin]
        public async Task RemoveAllowedLink(string link)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.allowedLinks == null || !settings.allowedLinks.Contains(link))
            {
                await ReplyAsync("Link is already not allowed");
                return;
            }
            settings.allowedLinks.Remove(link);
            if (settings.allowedLinks.Count == 0) settings.allowedLinks = null;
            settings.SaveToFile();
            await ReplyAsync("People will no longer be allowed to use " + link);
        }

        [Command("toggleinvitewarn")]
        [Summary("Toggles if a user is warned from sending an invite.")]
        [HasAdmin]
        public async Task ToggleInviteWarn()
        {
            IUserMessage message = await ReplyAsync("Trying to toggle");
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            settings.invitesAllowed = !settings.invitesAllowed;
            settings.SaveToFile();

            await message.ModifyAsync(msg => msg.Content = "set invites allowed to " + settings.invitesAllowed.ToString().ToLower());
        }

        [Command("togglezalgowarn"), Alias("togglezalgoallowed")]
        [Summary("Toggles if a user is warned from sending zalgo.")]
        [HasAdmin]
        public async Task ToggleZalgoWarn()
        {
            IUserMessage message = await ReplyAsync("Trying to toggle");
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            settings.zalgoAllowed = !settings.zalgoAllowed;
            settings.SaveToFile();

            await message.ModifyAsync(msg => msg.Content = "Set zalgo allowed to " + settings.zalgoAllowed.ToString().ToLower());
        }

        [Command("removeword")]
        [Summary("Removes a badword.")]
        [Alias("removebadword", "badremove", "removebadword")]
        [HasAdmin]
        public async Task RemoveBadWord(string word)
        {
            BadWordList badWordsClass = Context.Guild.LoadFromFile<BadWordList>(false);

            if (badWordsClass == null)
            {
                await ReplyAsync("No bad words are set");
                return;
            }
            BadWord badToRemove = badWordsClass.badWords.FirstOrDefault(badWord => badWord.word == word);
            if (badToRemove != null)
            {
                badWordsClass.badWords.Remove(badToRemove);
                badWordsClass.SaveToFile();

                await ReplyAsync($"Removed {word} from bad word list");
            }
            else
            {
                await ReplyAsync("Bad word list doesn't contain " + word);
            }
        }

        [Command("addword")]
        [Summary("Adds a badword.")]
        [Alias("addbadword", "wordadd", "badwordadd")]
        [HasAdmin]
        public async Task AddBadWord(string word, string euphemism = null, float size = 0.5f)
        {
            BadWord badWord = new BadWord
            {
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
        [Summary("Sets this channel as an announcement channel.")]
        public async Task AddAnouncementChannel()
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings.anouncementChannels.Contains(Context.Channel.Id))
            {
                await ReplyAsync("This is already an 'anouncement' channel");
                return;
            }
            settings.anouncementChannels.Add(Context.Channel.Id);
            settings.SaveToFile();
            await ReplyAsync("This channel is now an 'anouncement' channel");
        }

        [Command("removeanouncementchannel"), HasAdmin]
        [Summary("Sets this channel as a regular channel.")]
        public async Task RemoveAnouncementChannel()
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
            if (!settings?.anouncementChannels?.Contains(Context.Channel.Id) ?? true)
            {
                //Goes through various steps to check if 1. settings (for anouncement channels) exist 2. Current channel is in those settings
                await ReplyAsync("This already not an 'anouncement' channel");
                return;
            }
            settings.anouncementChannels.Remove(Context.Channel.Id);
            settings.SaveToFile();
            await ReplyAsync("This channel is now not an 'anouncement' channel");
        }

        [Command("togglenamefilter")]
        [Summary("Toggles if usernames and nicknames are filtered by the automod.")]
        [HasAdmin]
        public async Task ToggleNameFilter()
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            settings.moderateNames = !settings.moderateNames;
            settings.SaveToFile();
            string extra = "";
            if (settings.moderateNames) extra = " note: existing usernames and nicknames won't be filtered";
            await ReplyAsync("Set user name and nickname filtering to " + settings.moderateNames.ToString().ToLowerInvariant());
        }

        [Command("setmaxnewlines")]
        [Summary("Sets the amount of lines a single message may contain.")]
        [Alias("maxnewlinesset", "setmaximumnewlines")]
        [HasAdmin]
        public async Task SetMaximumNewLines(byte? amount)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            string reply;
            if (amount == null || amount < 0)
            {
                settings.maxNewLines = null;
                reply = "Disabled newline filtering";
            }
            else
            {
                settings.maxNewLines = amount;
                reply = "Set maximum amount of newlines to " + amount;
            }
            settings.SaveToFile();
            await ReplyAsync(reply);
        }

        [Command("whitelistguild")]
        [Summary("Removes a specific guild from being filtered by the automoderator.")]
        [Alias("addwhitelistguild", "whitelistguildinvite")]
        [HasAdmin]
        public async Task AddInviteWhitelist(ulong guildID)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (settings?.whitelistedForInvite == null) settings.whitelistedForInvite = new List<ulong>();
            else if (settings.whitelistedForInvite.Contains(guildID))
            {
                await ReplyAsync("Selected guild is already whitelisted");
                return;
            }
            settings.whitelistedForInvite.Add(guildID);
            settings.SaveToFile();
            await ReplyAsync("Invites leading to this server won't result in warns");
        }

        [Command("unwhitelistguild")]
        [Summary("Adds a specific guild to the filter.")]
        [Alias("removewhitelistguild", "unwhitelistguildinvite")]
        [HasAdmin]
        public async Task RemoveInviteWhitelist(ulong guildID)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (settings?.whitelistedForInvite == null) settings.whitelistedForInvite = new List<ulong>();
            else if (settings.whitelistedForInvite.Contains(guildID))
            {
                await ReplyAsync("Invites leading to selected server will already result in warns");
            }
            else
            {
                settings.whitelistedForInvite.Add(guildID);
                settings.SaveToFile();
                await ReplyAsync("Invites leading to selected server will now result in warns");
            }
        }
    }
}