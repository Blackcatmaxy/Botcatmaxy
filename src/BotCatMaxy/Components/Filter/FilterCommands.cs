using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotCatMaxy.Components.Interactivity;

namespace BotCatMaxy.Components.Filter
{
    [Name("Automoderation")]
    [Group("automod")]
    [Summary("Manages the automoderator.")]
    [Alias("automod", "auto -mod", "filter")]
    public class FilterCommands : InteractiveModule
    {
        public FilterCommands(IServiceProvider service) : base(service)
        {
        }

#nullable enable
        [Command("list")]
        [Summary("View filter information.")]
        [Alias("info")]
        [AdminOrDM]
        public async Task<RuntimeResult> ListAutoMod(string? extension = null)
        {
            var guild = await QueryMutualGuild();
            if (guild == null)
                return CommandResult.FromError("You have timed out or canceled.");

            var filterSettings = guild.LoadFromFile<FilterSettings>(false);
            var embed = new EmbedBuilder()
                .WithGuildAsAuthor(guild);

            if (filterSettings == null)
            {
                embed.AddField("Filter Settings", "Are not set.", true)
                     .WithColor(Color.Gold);
            }
            else
            {
                embed.AddField("Are invites restricted:", !filterSettings.invitesAllowed, true);
                if (filterSettings.allowedLinks?.Count is null or 0)
                {
                    embed.AddField("Link filter:", "Disabled.", true);
                }
                else
                {
                    string allowedLinks = filterSettings.allowedLinks.ListItems("\n");
                    embed.AddField("Link filter:", allowedLinks, true)
                         .WithColor(Color.Red);

                    if (filterSettings.allowedToLink?.Count is not null or 0)
                    {
                        var linkRoles = guild.Roles.Where(role => (role.Permissions.Administrator && !role.IsManaged)
                                                                     || filterSettings.allowedToLink.Contains(role.Id));
                        string linkRoleListed = linkRoles.Select(role => role.Name).ListItems("\n");

                        embed.AddField("Roles that can post links:", linkRoleListed, true);
                    }
                }

                string allowedCaps = filterSettings.allowedCaps > 0 ?
                    $"{filterSettings.allowedCaps}% in messages more than 5 characters long." :
                    "Capitalization is not filtered.";
                embed.AddField("Allowed capitalized characters:", allowedCaps, true);
                string? badUniEmojis = filterSettings.badUEmojis?.ListItems("");
                if (!string.IsNullOrWhiteSpace(badUniEmojis))
                {
                    embed.AddField("Banned Emojis:", badUniEmojis, true);
                }
                if (filterSettings.moderateNames)
                    embed.AddField("Name moderation", "True", true);
                if (filterSettings.maxNewLines is > 0)
                    embed.AddField("Maximum new lines", $"{filterSettings.maxNewLines.Value} new lines", true);
            }

            var badWordList = guild.LoadFromFile<BadWordList>(false)?.badWords;
            if (badWordList?.Count is > 0)
            {
                BadWordSets badWords = new BadWordSets(guild);
                bool useExplicit = false;
                if (extension?.ToLowerInvariant() is "explicit" or "e")
                {
                    var user = await (guild.GetUserAsync(Context.Message.Author.Id));
                    if (user.CanWarn())
                    {
                        useExplicit = true;
                    }
                    else
                    {
                        useExplicit = await TryConfirmation("Are you sure you want to view the explicit filter?", TimeSpan.FromMinutes(1));
                    }
                }

                AddBadWordsToEmbed(embed, badWords, useExplicit);
            }
            return CommandResult.FromSuccess("The symbol '¤' next to a word means that you can be warned for any text that contains the bad word", embed: embed.Build());
        }

        public void AddBadWordsToEmbed(EmbedBuilder embed, BadWordSets badWords, bool useExplicit)
        {
            string list = "";
            foreach (ImmutableHashSet<BadWord> group in badWords.Grouped)
            {
                if (group.Count == 0)
                    throw new InvalidOperationException("Invalid bad words state.");
                list += RenderBadWordGroup(group, useExplicit);
                list += '\n';
            }
            string listName = useExplicit ? "Bad words with euphemisms" : "Bad word euphemisms (words without euphemisms not shown)";
            embed.AddField(listName, list[..^1], false);
        }

        public static string RenderBadWordGroup(ImmutableHashSet<BadWord> group, bool useExplicit)
        {
            var category = group.First();
            string word;
            if (group.Count == 1 || group.All(badWord => badWord.Size == category.Size))
            {
                word = $"[{category.Size}x] ";
            }
            else
            {
                IEnumerable<float> sizesEnumerable = group.Select(badword => badword.Size);
                float[] sizes = sizesEnumerable as float[] ?? sizesEnumerable.ToArray();
                word = $"[{sizes.Min()}-{sizes.Max()}x] ";
            }

            if (useExplicit)
            {
                if (!string.IsNullOrWhiteSpace(category.Euphemism))
                    word += $"{category.Euphemism} ";
                word += $"({group.Select(badWord => $"{badWord.Word}{(badWord.PartOfWord ? "¤" : "")}").ListItems(", ")})";
            }
            else if (!string.IsNullOrWhiteSpace(category.Euphemism))
                word = category.Euphemism;
            if (category.PartOfWord && (!string.IsNullOrWhiteSpace(category.Euphemism) && !useExplicit))
            {
                word += "¤";
            }

            return word;
        }
#nullable disable

        [HasAdmin]
        [Command("maxemoji"), Alias("setmaxemoji")]
        [Summary("Set a number of max emojis a user may send in a single message.")]
        public async Task AllowEmojis(uint amount)
        {
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
            if (amount == settings.maxEmojis)
            {
                await ReplyAsync("The selected value is already set");
                return;
            }
            settings.maxEmojis = amount;
            settings.SaveToFile();
            string extraInfo = "";
            if (settings.allowedToLink?.Count is not null or 0) extraInfo = " except by role allowed to link";
            if (amount == 0) await ReplyAsync("No emojis are allowed" + extraInfo);
            else await ReplyAsync($"Max {amount} emojis are allowed{extraInfo}");
        }

        [HasAdmin]
        [Command("allowemoji"), Alias("setmaxemojis")]
        [Summary("Set a number of max emojis a user may send in a single message.")]
        public async Task SetMaxEmojis(string amount)
        {
            FilterSettings settings;
            switch (amount.ToLower())
            {
                case "null":
                case "none":
                case "disable":
                    settings = Context.Guild.LoadFromFile<FilterSettings>(false);
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
                    settings = Context.Guild.LoadFromFile<FilterSettings>(true);
                    settings.maxEmojis = 0;
                    settings.SaveToFile();
                    string extraInfo = "";
                    if (settings.allowedToLink?.Count is not null or 0) extraInfo = " except by role allowed to link";
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
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
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
            var settings = Context.Guild.LoadFromFile<FilterSettings>(false);
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
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);

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
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);

            if (settings.allowedToLink == null) settings.allowedToLink = new HashSet<ulong>();
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
            var settings = Context.Guild.LoadFromFile<FilterSettings>();

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
        
#nullable enable
        [Command("ToggleContainBadWord")]
        [Summary("Toggle strict filtering and checks if a bad word is sent even if inside another word.")]
        [Alias("togglecontainword", "togglecontainword")]
        [HasAdmin()]
        public async Task<RuntimeResult> ToggleContainBadWord(string word)
        {
            var file = Context.Guild.LoadFromFile<BadWordList>(false);
            var badWords = file?.badWords;
            if (badWords?.Count is null or 0)
                return CommandResult.FromError("No badwords have been set yet.");

            var badWord = badWords.FirstOrDefault(bw => bw.Word == word);
            if (badWord == null)
                return CommandResult.FromError("Badword was not found.");

            badWords.Remove(badWord);
            badWords.Add(badWord with {PartOfWord = !badWord.PartOfWord});
            file.SaveToFile();
            string status = $"Set badword to {(badWord.PartOfWord ? "" : "not ")}be filtered even if it's inside of another word.";
            return CommandResult.FromSuccess(status);
        }
#nullable disable

        [Command("channeltoggle")]
        [Summary("Toggles if this channel is exempt from automoderation.")]
        [HasAdmin]
        public async Task ToggleAutoMod()
        {
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);

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
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
            if (settings.filterIgnored == null) settings.filterIgnored = new HashSet<ulong>();
            else if (settings.filterIgnored.Contains(role.Id))
            {
                await ReplyAsync($"Role '{role.Name}' is already not able to be warned");
                return;
            }
            settings.filterIgnored.Add(role.Id);
            settings.SaveToFile();
            await ReplyAsync($"Role '{role.Name}' will not be able to be warned now");
        }

        [Command("removeignoredrole")]
        [Summary("Allows a user from being warned by the automoderator.")]
        [HasAdmin]
        public async Task RemovedWarnIgnoredRole(SocketRole role)
        {
            var settings = Context.Guild.LoadFromFile<FilterSettings>();
            if (settings == null || settings.filterIgnored == null) settings.filterIgnored = new HashSet<ulong>();
            else if (settings.filterIgnored.Contains(role.Id))
            {
                await ReplyAsync($"Role '{role.Name}' is already able to be warned");
            }
            else
            {
                settings.filterIgnored.Add(role.Id);
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
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
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
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
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
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
            settings.invitesAllowed = !settings.invitesAllowed;
            settings.SaveToFile();

            await message.ModifyAsync(msg => msg.Content = "Set invites allowed to " + settings.invitesAllowed.ToString().ToLower());
        }

        [Command("togglezalgowarn"), Alias("togglezalgoallowed")]
        [Summary("Toggles if a user is warned from sending zalgo.")]
        [HasAdmin]
        public async Task ToggleZalgoWarn()
        {
            IUserMessage message = await ReplyAsync("Trying to toggle");
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
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
            BadWord badToRemove = badWordsClass.badWords.FirstOrDefault(badWord => badWord.Word == word);
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
            BadWord badWord = new BadWord(word, euphemism, size);
            BadWordList badWordsClass = Context.Guild.LoadFromFile<BadWordList>(true);
            badWordsClass.badWords.Add(badWord);
            badWordsClass.SaveToFile();

            await ReplyAsync($"Added {badWord.Word}{((badWord.Euphemism != null) ? $", also known as {badWord.Euphemism}" : "")} to bad word list");
        }

        [Command("addannouncementchannel"), HasAdmin]
        [Alias("addanouncementchannel")] //Such a common misspelling this was the name for a long time with no reports
        [Summary("Sets this channel as an 'announcement' channel.")]
        public async Task AddAnnouncementChannel()
        {
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
            if (settings.announcementChannels.Contains(Context.Channel.Id))
            {
                await ReplyAsync("This is already an 'announcement' channel");
                return;
            }
            settings.announcementChannels.Add(Context.Channel.Id);
            settings.SaveToFile();
            await ReplyAsync("This channel is now an 'announcement' channel");
        }

        [Command("removeannouncementchannel"), HasAdmin]
        [Alias("removeanouncementchannel")] //Such a common misspelling this was the name for a long time with no reports
        [Summary("Sets this channel as a regular channel.")]
        public async Task RemoveAnnouncementChannel()
        {
            var settings = Context.Guild.LoadFromFile<FilterSettings>(false);
            if (!settings?.announcementChannels?.Contains(Context.Channel.Id) ?? true)
            {
                //Goes through various steps to check if 1. settings (for announcement channels) exist 2. Current channel is in those settings
                await ReplyAsync("This already not an 'announcement' channel");
                return;
            }
            settings.announcementChannels.Remove(Context.Channel.Id);
            settings.SaveToFile();
            await ReplyAsync("This channel is now not an 'announcement' channel");
        }

        [Command("togglenamefilter")]
        [Summary("Toggles if usernames and nicknames are filtered by the automod.")]
        [HasAdmin]
        public async Task ToggleNameFilter()
        {
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
            settings.moderateNames = !settings.moderateNames;
            settings.SaveToFile();
            await ReplyAsync("Set user name and nickname filtering to " + settings.moderateNames.ToString().ToLowerInvariant());
        }

        [Command("setmaxnewlines")]
        [Summary("Sets the amount of lines a single message may contain.")]
        [Alias("maxnewlinesset", "setmaximumnewlines")]
        [HasAdmin]
        public async Task SetMaximumNewLines(byte? amount)
        {
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
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
            var settings = Context.Guild.LoadFromFile<FilterSettings>(true);
            if (settings?.whitelistedForInvite == null) settings.whitelistedForInvite = new HashSet<ulong>();
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
            var settings = Context.Guild.LoadFromFile<FilterSettings>();
            if (settings?.whitelistedForInvite == null) settings.whitelistedForInvite = new HashSet<ulong>();
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