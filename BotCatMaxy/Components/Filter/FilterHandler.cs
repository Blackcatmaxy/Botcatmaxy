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
        const string inviteRegex = @"(?:http|https?:\/\/)?(?:www\.)?(?:discord\.(?:gg|io|me|li|com)|discord(?:app)?\.com\/invite)\/(\S+)";
        RegexOptions regexOptions;

        readonly DiscordSocketClient client;
        public FilterHandler(DiscordSocketClient client)
        {
            this.client = client;
            client.MessageReceived += CheckMessage;
            client.MessageUpdated += HandleEdit;
            client.ReactionAdded += HandleReaction;
            client.UserJoined += HandleUserJoin;
            client.UserUpdated += HandleUserChange;
            client.GuildMemberUpdated += HandleUserChange;
            regexOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            new LogMessage(LogSeverity.Info, "Filter", "Filter is active").Log();
        }

        public async Task HandleEdit(Cacheable<IMessage, ulong> oldMessage, SocketMessage editedMessage, ISocketMessageChannel channel)
            => await Task.Run(() => CheckMessage(editedMessage)).ConfigureAwait(false);

        public async Task HandleReaction(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction)
            => await Task.Run(() => CheckReaction(cachedMessage, channel, reaction)).ConfigureAwait(false);

        public async Task HandleUserJoin(SocketGuildUser user)
            => await Task.Run(async () => CheckNameInGuild(user, user.Username, user.Guild));

        public async Task HandleGuildUserChange(SocketGuildUser old, SocketGuildUser updated)
        {
            if (updated.Nickname != old.Nickname)
                await Task.Run(async () => CheckNameInGuild(updated, updated.Nickname, updated.Guild));
        }

        public async Task HandleUserChange(SocketUser old, SocketUser updated)
        {
            if (updated.Username != old.Username)
            {
                foreach (SocketGuild guild in updated.MutualGuilds)
                {
                    await Task.Run(async () => CheckNameInGuild(updated, updated.Username, guild));
                }
            }
        }

        public async Task HandleMessage(SocketMessage message)
            => await Task.Run(() => CheckMessage(message)).ConfigureAwait(false);

        public async Task CheckNameInGuild(IUser user, string name, SocketGuild guild)
        {
            try
            {
                if (!guild.CurrentUser.GuildPermissions.KickMembers) return;
                ModerationSettings settings = guild.LoadFromFile<ModerationSettings>(false);
                //Has to check if not equal to true since it's nullable
                if (settings?.moderateNames != true) return;

                SocketGuildUser gUser = user as SocketGuildUser ?? guild.GetUser(user.Id);
                if (gUser.CantBeWarned() || !gUser.CanActOn(guild.CurrentUser))
                    return;

                BadWord detectedBadWord = name.CheckForBadWords(guild.LoadFromFile<BadWordList>(false)?.badWords.ToArray());
                if (detectedBadWord == null) return;

                LogSettings logSettings = guild.LoadFromFile<LogSettings>(false);
                if (logSettings?.logChannel != null && guild.TryGetChannel(logSettings.logChannel ?? 0, out IGuildChannel channel))
                {
                    EmbedBuilder embed = new EmbedBuilder();
                    embed.WithColor(Color.DarkMagenta);
                    embed.WithAuthor(user);
                    embed.WithTitle("User kicked for bad username");
                    embed.WithDescription($"Name '{name}' contained '{detectedBadWord.word}'");
                    embed.WithCurrentTimestamp();
                    embed.WithFooter("User ID: " + user.Id);
                    await (channel as SocketTextChannel).SendMessageAsync(embed: embed.Build());
                }
                //If user's DMs aren't blocked
                if (user.TryNotify($"Your username contains a filtered word ({detectedBadWord.word}). Please change it before rejoining {guild.Name} Discord"))
                {
                    await gUser.KickAsync($"Username '{name}' triggered autofilter for '{detectedBadWord.word}'");
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
                    await gUser.KickAsync($"Username '{name}' triggered autofilter for '{detectedBadWord.word}'");
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
            if ((reaction.User.IsSpecified && reaction.User.Value.IsBot) || !(channel is SocketGuildChannel))
            {
                return; //Makes sure it's not logging a message from a bot and that it's in a discord server
            }
            SocketGuildChannel chnl = channel as SocketGuildChannel;
            SocketGuild guild = chnl?.Guild;
            if (guild == null) return;
            try
            {
                IUserMessage message = await cachedMessage.GetOrDownloadAsync();
                SocketCommandContext context = new SocketCommandContext(client, message as SocketUserMessage);
                ModerationSettings settings = guild.LoadFromFile<ModerationSettings>(false);
                SocketGuildUser gUser = guild.GetUser(reaction.UserId);
                var Guild = chnl.Guild;
                if (settings?.badUEmojis?.Count == null || settings.badUEmojis.Count == 0 || (reaction.User.Value as SocketGuildUser).CantBeWarned() || reaction.User.Value.IsBot)
                {
                    return;
                }
                if (settings.badUEmojis.Select(emoji => new Emoji(emoji)).Contains(reaction.Emote))
                {
                    await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                    await context.FilterPunish(gUser, "bad reaction used", settings, delete: false, warnSize: 1);
                }
            }
            catch (Exception e)
            {
                await e.LogFilterError("reaction", guild);
            }
        }

        public async Task CheckMessage(SocketMessage message)
        {
            if (message.Author.IsBot || !(message.Channel is SocketGuildChannel) || !(message is SocketUserMessage) || string.IsNullOrWhiteSpace(message.Content))
            {
                return; //Makes sure it's not logging a message from a bot and that it's in a discord server
            }
            SocketCommandContext context = new SocketCommandContext(client, message as SocketUserMessage);
            SocketGuildChannel chnl = message.Channel as SocketGuildChannel;
            if (chnl?.Guild == null || (message.Author as SocketGuildUser).CantBeWarned()) return;
            var guild = chnl.Guild;

            try
            {
                ModerationSettings modSettings = guild.LoadFromFile<ModerationSettings>();
                SocketGuildUser gUser = message.Author as SocketGuildUser;
                List<BadWord> badWords = guild.LoadFromFile<BadWordList>()?.badWords;

                string msgContent = message.Content;
                if (modSettings != null)
                {
                    if (modSettings.channelsWithoutAutoMod != null && modSettings.channelsWithoutAutoMod.Contains(chnl.Id))
                        return; //Returns if channel is set as not using automod

                    //Checks if a message contains too many "newlines"
                    if (modSettings.maxNewLines != null)
                    {
                        //Gets number of "newlines"
                        int newLines = context.Message.Content.Count(c => c == '\n');
                        if (newLines > modSettings.maxNewLines.Value)
                        {
                            await context.FilterPunish("too many newlines", modSettings, (newLines - modSettings.maxNewLines.Value) * 0.5f);
                            return;
                        }
                    }

                    //Checks if a message contains an invite
                    if (!modSettings.invitesAllowed)
                    {
                        MatchCollection matches = Regex.Matches(message.Content, inviteRegex, regexOptions);
                        var invites = matches.Select(async match => await client.GetInviteAsync(match.Value)).Select(match => match.Result);
                        if (invites.Any())
                            foreach (RestInviteMetadata invite in invites)
                                if (invite?.GuildId != null && !modSettings.whitelistedForInvite.Contains(invite.GuildId.Value))
                                {
                                    await context.FilterPunish("Posted Invite", modSettings);
                                    return;
                                }
                    }

                    //Checks if a message contains ugly, unwanted text t̨̠̱̭͓̠ͪ̈́͌ͪͮ̐͒h̲̱̯̀͂̔̆̌͊ͅà̸̻͌̍̍ͅt͕̖̦͂̎͂̂ͮ͜ ̲͈̥͒ͣ͗̚l̬͚̺͚͎̆͜ͅo͔̯̖͙ͩõ̲̗̎͆͜k̦̭̮̺ͮ͆̀ ͙̍̂͘l̡̮̱̤͍̜̲͙̓̌̐͐͂̓i͙̬ͫ̀̒͑̔͐k̯͇̀ͭe̎͋̓́ ̥͖̼̬ͪ̆ṫ͏͕̳̞̯h̛̼͔ͩ̑̿͑i͍̲̽ͮͪsͦ͋ͦ̌͗ͭ̋
                    //Props to Mathias Bynens for the regex string
                    const string zalgoRegex = @"([\0-\u02FF\u0370-\u1AAF\u1B00-\u1DBF\u1E00-\u20CF\u2100-\uD7FF\uE000-\uFE1F\uFE30-\uFFFF]|[\uD800-\uDBFF][\uDC00-\uDFFF]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?:[^\uD800-\uDBFF]|^)[\uDC00-\uDFFF])([\u0300-\u036F\u1AB0-\u1AFF\u1DC0-\u1DFF\u20D0-\u20FF\uFE20-\uFE2F]+)";
                    if (modSettings.zalgoAllowed == false)
                    {
                        MatchCollection matches = Regex.Matches(message.Content, zalgoRegex, regexOptions);
                        if (matches.Any())
                        {
                            await context.FilterPunish("zalgo usage", modSettings);
                            return;
                        }
                    }

                    const string linkRegex = @"((?:https?|steam):\/\/[^\s<]+[^<.,:;" + "\"\'\\]\\s])";
                    MatchCollection linkMatches = Regex.Matches(message.Content, linkRegex, regexOptions);
                    //if (matches != null && matches.Count > 0) await new LogMessage(LogSeverity.Info, "Filter", "Link detected").Log();
                    foreach (Match match in linkMatches)
                    {
                        if (msgContent.Equals(match.Value, StringComparison.InvariantCultureIgnoreCase)) return;
                        msgContent = msgContent.Replace(match.Value, "", StringComparison.InvariantCultureIgnoreCase);
                        //Checks for links
                        if ((modSettings.allowedLinks != null && modSettings.allowedLinks.Count > 0) && (modSettings.allowedToLink == null || !gUser.RoleIDs().Intersect(modSettings.allowedToLink).Any()))
                        {
                            if (!modSettings.allowedLinks.Any(s => match.ToString().ToLower().Contains(s.ToLower())))
                            {
                                await context.FilterPunish("Using unauthorized links", modSettings, 1);
                                return;
                            }
                        }
                    }

                    //Check for emojis
                    if (modSettings.badUEmojis.NotEmpty() && modSettings.badUEmojis.Any(s => message.Content.Contains(s)))
                    {
                        await context.FilterPunish("Bad emoji used", modSettings, 0.8f);
                        return;
                    }

                    if (modSettings.allowedCaps > 0 && message.Content.Length > 5)
                    {
                        uint amountCaps = 0;
                        foreach (char c in message.Content)
                        {
                            if (char.IsUpper(c))
                            {
                                amountCaps++;
                            }
                        }
                        if (((amountCaps / (float)message.Content.Length) * 100) >= modSettings.allowedCaps)
                        {
                            await context.FilterPunish("Excessive caps", modSettings, 0.3f);
                            return;
                        }
                    }
                } //End of stuff from mod settings

                BadWord detectedBadWord = msgContent.CheckForBadWords(badWords?.ToArray());
                if (detectedBadWord != null)
                {
                    if (!string.IsNullOrEmpty(detectedBadWord.euphemism))
                    {
                        await context.FilterPunish("Bad word used (" + detectedBadWord.euphemism + ")", modSettings, detectedBadWord.size);
                        return;
                    }
                    else
                    {
                        await context.FilterPunish("Bad word used", modSettings, detectedBadWord.size);
                        return;
                    }
                }

            }
            catch (Exception e)
            {
                await e.LogFilterError("message", guild);
            }
        }
    }
}

