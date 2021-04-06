using BotCatMaxy.Components.Filter;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Moderation;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BotCatMaxy.Startup
{
    public class FilterHandler
    {
        public const string inviteRegex = @"(?:https?:\/\/)?(?:\w+\.)?discord(?:(?:app)?\.com\/invite|\.gg)\/([A-Za-z0-9-]+)";
        private const RegexOptions regexOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private readonly IDiscordClient client;

        public FilterHandler(IDiscordClient client)
        {
            this.client = client;
            if (client is BaseSocketClient socketClient)
            {
                socketClient.MessageReceived += HandleMessage;
                socketClient.MessageUpdated += HandleEdit;
                socketClient.ReactionAdded += HandleReaction;
                socketClient.UserJoined += HandleUserJoin;
                socketClient.UserUpdated += HandleUserChange;
            }

            new LogMessage(LogSeverity.Info, "Filter", "Filter is active").Log();
        }

        private Task HandleEdit(Cacheable<IMessage, ulong> oldMessage, SocketMessage editedMessage, ISocketMessageChannel channel)
        {
            Task.Run(() => CheckMessage(editedMessage));
            return Task.CompletedTask;
        }

        public Task HandleReaction(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction)
        {
            Task.Run(() => CheckReaction(cachedMessage, channel, reaction));
            return Task.CompletedTask;
        }

        public Task HandleUserJoin(SocketGuildUser user)
        {
            Task.Run(() => CheckNameInGuild(user, user.Username, user.Guild));
            return Task.CompletedTask;
        }

        public Task HandleUserChange(SocketUser old, SocketUser updated)
        {
            if (updated.Username != old.Username)
            {
                foreach (SocketGuild guild in updated.MutualGuilds)
                {
                    Task.Run(() => CheckNameInGuild(updated, updated.Username, guild));
                }
            }
            return Task.CompletedTask;
        }

        public Task HandleMessage(SocketMessage message)
        {
            Task.Run(() => CheckMessage(message));
            return Task.CompletedTask;
        }

        public async Task CheckNameInGuild(IUser user, string name, IGuild guild)
        {
            try
            {
                var currentUser = await guild.GetCurrentUserAsync();
                if (!currentUser.GuildPermissions.KickMembers) return;
                var settings = guild.LoadFromFile<FilterSettings>(false);
                //Has to check if not equal to true since it's nullable
                if (settings?.moderateNames != true) return;

                IGuildUser gUser = user as IGuildUser ?? await guild.GetUserAsync(user.Id);
                if (gUser.CantBeWarned() || !gUser.CanActOn(currentUser))
                    return;

                BadWord detectedBadWord = name.CheckForBadWords(guild.LoadFromFile<BadWordList>(false)?.badWords.ToArray()).word;
                if (detectedBadWord == null) return;

                LogSettings logSettings = guild.LoadFromFile<LogSettings>(false);
                if (logSettings?.logChannel != null && guild.TryGetChannel(logSettings.logChannel ?? 0, out IGuildChannel channel))
                {
                    EmbedBuilder embed = new EmbedBuilder();
                    embed.WithColor(Color.DarkMagenta);
                    embed.WithAuthor(user);
                    embed.WithTitle("User kicked for bad username");
                    embed.WithDescription($"Name '{name}' contained '{detectedBadWord.Word}'");
                    embed.WithCurrentTimestamp();
                    embed.WithFooter("User ID: " + user.Id);
                    await (channel as SocketTextChannel).SendMessageAsync(embed: embed.Build());
                }
                //If user's DMs aren't blocked
                if (await user.TryNotify($"Your username contains a filtered word ({detectedBadWord.Word}). Please change it before rejoining {guild.Name} Discord"))
                {
                    await gUser.KickAsync($"Username '{name}' triggered autofilter for '{detectedBadWord.Word}'");
                    user.Id.AddWarn(1, "Username with filtered word", guild, null);
                    return;
                }//If user's DMs are blocked
                if (logSettings != null)
                {
                    if (guild.TryGetTextChannel(logSettings.backupChannel, out ITextChannel backupChannel))
                        await backupChannel.SendMessageAsync($"{user.Mention} your username contains a bad word but your DMs are closed. Please clean up your username before rejoining");
                    else if (guild.TryGetTextChannel(logSettings.pubLogChannel, out ITextChannel pubChannel))
                        await pubChannel.SendMessageAsync($"{user.Mention} your username contains a bad word but your DMs are closed. Please clean up your username before rejoining");
                    await Task.Delay(10000);
                    await gUser.KickAsync($"Username '{name}' triggered autofilter for '{detectedBadWord.Word}'");
                    user.Id.AddWarn(1, "Username with filtered word (Note: DMs closed)", guild, null);

                }
            }
            catch (Exception e)
            {
                await e.LogFilterError("username", guild);
            }
        }

        public async Task CheckReaction(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if ((reaction.User.IsSpecified && reaction.User.Value.IsBot) || !(channel is IGuildChannel))
            {
                return; //Makes sure it's not logging a message from a bot and that it's in a discord server
            }
            SocketGuildChannel chnl = channel as SocketGuildChannel;
            SocketGuild guild = chnl?.Guild;
            if (guild == null) return;
            try
            {
                //Needed to do our own get instead of cachedMessage.GetOrDownloadAsync() because this can be ISystenMessage and not just IUserMessage
                IMessage message = await channel.GetMessageAsync(cachedMessage.Id);
                ReactionContext context = new ReactionContext(client, message);
                var settings = guild.LoadFromFile<FilterSettings>(false);
                SocketGuildUser gUser = guild.GetUser(reaction.UserId);
                var Guild = chnl.Guild;
                if (settings?.badUEmojis?.Count == null || settings.badUEmojis.Count == 0 || (reaction.User.Value as SocketGuildUser).CantBeWarned() || reaction.User.Value.IsBot)
                {
                    return;
                }
                if (settings.badUEmojis.Contains(reaction.Emote.Name))
                {
                    await message.RemoveAllReactionsForEmoteAsync(reaction.Emote);
                    await context.FilterPunish(gUser, $"bad reaction used ({reaction.Emote.Name})", guild.LoadFromFile<ModerationSettings>(), settings, null, delete: false, warnSize: 1);
                }
            }
            catch (Exception e)
            {
                await e.LogFilterError("reaction", guild);
            }
        }

        public async Task CheckMessage(IMessage message, ICommandContext context = null)
        {
            if (message.Author.IsBot || message.Channel is not IGuildChannel chnl || message is not IUserMessage userMessage || string.IsNullOrWhiteSpace(message.Content))
            {
                return; //Makes sure it's not logging a message from a bot and that it's in a discord server
            }
            context ??= new SocketCommandContext((DiscordSocketClient)client, (SocketUserMessage)message);
            IGuildUser gUser = message.Author as IGuildUser;
            if (chnl?.Guild == null || gUser.CantBeWarned()) return;
            var guild = chnl.Guild;

            try
            {
                ModerationSettings modSettings = guild.LoadFromFile<ModerationSettings>();
                var filterSettings = guild.LoadFromFile<FilterSettings>();
                List<BadWord> badWords = guild.LoadFromFile<BadWordList>()?.badWords;

                string msgContent = message.Content;
                if (modSettings != null && filterSettings != null)
                {
                    if (filterSettings.channelsWithoutAutoMod != null && filterSettings.channelsWithoutAutoMod.Contains(chnl.Id))
                        return; //Returns if channel is set as not using automod

                    //Checks if a message contains too many "newlines"
                    if (filterSettings.maxNewLines != null)
                    {
                        //Gets number of "newlines"
                        int newLines = context.Message.Content.Count(c => c == '\n');
                        if (newLines > filterSettings.maxNewLines.Value)
                        {
                            await context.FilterPunish("too many newlines", modSettings, filterSettings, null, warnSize: (newLines - filterSettings.maxNewLines.Value) * 0.5f);
                            return;
                        }
                    }

                    //Checks if a message contains an invite
                    if (!filterSettings.invitesAllowed)
                    {
                        MatchCollection matches = Regex.Matches(message.Content, inviteRegex, regexOptions);
                        foreach (Match match in matches)
                        {
                            var invite = await client.GetInviteAsync(match.Value);
                            if (invite?.GuildId != null && !filterSettings.whitelistedForInvite.Contains(invite.GuildId.Value))
                            {
                                await context.FilterPunish("Posted Invite", modSettings, filterSettings, match.Value, match.Index);
                                return;
                            }
                        }
                    }

                    //Checks if a message contains ugly, unwanted text t̨̠̱̭͓̠ͪ̈́͌ͪͮ̐͒h̲̱̯̀͂̔̆̌͊ͅà̸̻͌̍̍ͅt͕̖̦͂̎͂̂ͮ͜ ̲͈̥͒ͣ͗̚l̬͚̺͚͎̆͜ͅo͔̯̖͙ͩõ̲̗̎͆͜k̦̭̮̺ͮ͆̀ ͙̍̂͘l̡̮̱̤͍̜̲͙̓̌̐͐͂̓i͙̬ͫ̀̒͑̔͐k̯͇̀ͭe̎͋̓́ ̥͖̼̬ͪ̆ṫ͏͕̳̞̯h̛̼͔ͩ̑̿͑i͍̲̽ͮͪsͦ͋ͦ̌͗ͭ̋
                    //Props to Mathias Bynens for the regex string
                    const string zalgoRegex = @"([\0-\u02FF\u0370-\u1AAF\u1B00-\u1DBF\u1E00-\u20CF\u2100-\uD7FF\uE000-\uFE1F\uFE30-\uFFFF]|[\uD800-\uDBFF][\uDC00-\uDFFF]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?:[^\uD800-\uDBFF]|^)[\uDC00-\uDFFF])([\u0300-\u036F\u1AB0-\u1AFF\u1DC0-\u1DFF\u20D0-\u20FF\uFE20-\uFE2F]+)";
                    if (filterSettings.zalgoAllowed == false)
                    {
                        MatchCollection matches = Regex.Matches(message.Content, zalgoRegex, regexOptions);
                        if (matches.Any())
                        {
                            await context.FilterPunish("zalgo usage", modSettings, filterSettings, null);
                            return;
                        }
                    }

                    //Check for links if setting enabled and user is not allowed to link
                    if (filterSettings.allowedLinks?.Count is not null and not 0 && (filterSettings.allowedToLink == null || !gUser.RoleIds.Intersect(filterSettings.allowedToLink).Any()))
                    {
                        const string linkRegex = @"((?:https?|steam):\/\/[^\s<]+[^<.,:;" + "\"\'\\]\\s])";
                        MatchCollection linkMatches = Regex.Matches(message.Content, linkRegex, regexOptions);
                        //if (matches != null && matches.Count > 0) await new LogMessage(LogSeverity.Info, "Filter", "Link detected").Log();
                        foreach (Match match in linkMatches)
                        {
                            if (msgContent.Equals(match.Value, StringComparison.InvariantCultureIgnoreCase)) return;
                            msgContent = msgContent.Replace(match.Value, "", StringComparison.InvariantCultureIgnoreCase);
                            //Checks for links

                            if (!filterSettings.allowedLinks.Any(s => match.Value.Contains(s, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                await context.FilterPunish("Using unauthorized links", modSettings, filterSettings, match.Value, match.Index, warnSize: 1);
                                return;
                            }
                        }
                    }

                    //Check for emojis
                    if (filterSettings.badUEmojis?.Count is not null and not 0 && filterSettings.badUEmojis.Any(s => message.Content.Contains(s)))
                    {
                        await context.FilterPunish("Bad emoji used", modSettings, filterSettings, null, warnSize: 0.8f);
                        return;
                    }

                    if (filterSettings.allowedCaps > 0 && message.Content.Length > 5)
                    {
                        uint amountCaps = 0;
                        foreach (char c in message.Content)
                        {
                            if (char.IsUpper(c))
                            {
                                amountCaps++;
                            }
                        }
                        if (((amountCaps / (float)message.Content.Length) * 100) >= filterSettings.allowedCaps)
                        {
                            await context.FilterPunish("Excessive caps", modSettings, filterSettings, null, warnSize: 0.3f);
                            return;
                        }
                    }
                } //End of stuff from mod settings

                var badWordResult = msgContent.CheckForBadWords(badWords?.ToArray());
                var detectedBadWord = badWordResult.word;
                if (detectedBadWord != null)
                {
                    if (!string.IsNullOrEmpty(detectedBadWord.Euphemism))
                    {
                        await context.FilterPunish($"Bad word used ({detectedBadWord.Euphemism})", modSettings, filterSettings, detectedBadWord.Word, badWordResult.index, detectedBadWord.Size);
                        return;
                    }
                    else
                    {
                        await context.FilterPunish("Bad word used", modSettings, filterSettings, detectedBadWord.Word, badWordResult.index, detectedBadWord.Size);
                        return;
                    }
                }

            }
            catch (Exception e)
            {
#if DEBUG
                throw;
#else
                await e.LogFilterError("message", guild);
#endif
            }
        }
    }
}

