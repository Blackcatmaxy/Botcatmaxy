using BotCatMaxy;
using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Moderation;
using Discord;
using Interactivity;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Interactivity.Confirmation;

namespace BotCatMaxy
{
    [Name("Moderation")]
    public class ModerationCommands : ModuleBase<ICommandContext>
    {
#if !TEST
    [DontInject]
#endif
        public InteractivityService Interactivity { get; set; }

        [Command("warn")]
        [Summary("Warn a user with an option reason.")]
        public async Task WarnUserAsync([RequireHierarchy] UserRef userRef, [Remainder] string reason)
        {
            IUserMessage logMessage = await DiscordLogging.LogWarn(Context.Guild, Context.Message.Author, userRef.ID, reason, Context.Message.GetJumpUrl());
            WarnResult result = await userRef.Warn(1, reason, Context.Channel as ITextChannel, logLink: logMessage?.GetJumpUrl());

            if (result.success)
                Context.Message.DeleteOrRespond($"{userRef.Mention()} has gotten their {result.warnsAmount.Suffix()} infraction for {reason}", Context.Guild);
            else
            {
                if (logMessage != null)
                {
                    DiscordLogging.deletedMessagesCache.Enqueue(logMessage.Id);
                    await logMessage.DeleteAsync();
                }
                await ReplyAsync(result.description.Truncate(1500));
            }
        }

        [RequireContext(ContextType.Guild)]
        [Command("warn")]
        [Summary("Warn a user with a specific size, along with an option reason.")]
        [CanWarn()]
        public async Task WarnWithSizeUserAsync([RequireHierarchy] UserRef userRef, float size, [Remainder] string reason)
        {
            IUserMessage logMessage = await DiscordLogging.LogWarn(Context.Guild, Context.Message.Author, userRef.ID, reason, Context.Message.GetJumpUrl());
            WarnResult result = await userRef.Warn(size, reason, Context.Channel as ITextChannel, logLink: logMessage?.GetJumpUrl());
            if (result.success)
                Context.Message.DeleteOrRespond($"{userRef.Mention()} has gotten their {result.warnsAmount.Suffix()} infraction for {reason}", Context.Guild);
            else
            {
                if (logMessage != null)
                {
                    DiscordLogging.deletedMessagesCache.Enqueue(logMessage.Id);
                    await logMessage.DeleteAsync();
                }
                await ReplyAsync(result.description.Truncate(1500));
            }
        }

        [Command("dmwarns", RunMode = RunMode.Async)]
        [Summary("Views a user's infractions.")]
        [RequireContext(ContextType.DM, ErrorMessage = "This command now only works in the bot's DMs")]
        [Alias("dminfractions", "dmwarnings", "warns", "infractions", "warnings")]
        public async Task DMUserWarnsAsync(UserRef userRef = null, int amount = 50)
        {
            if (amount < 1)
            {
                await ReplyAsync("Why would you want to see that many infractions?");
                return;
            }
            
            var mutualGuilds = (await Context.Message.Author.GetMutualGuildsAsync(Context.Client)).ToArray();
            if (userRef == null)
                userRef = new UserRef(Context.Message.Author);

            var guildsEmbed = new EmbedBuilder();
            guildsEmbed.WithTitle("Reply with the number next to the guild you want to check the infractions from");

            for (int i = 0; i < mutualGuilds.Length; i++)
            {
                guildsEmbed.AddField($"[{i + 1}] {mutualGuilds[i].Name}", mutualGuilds[i].Id);
            }
            await ReplyAsync(embed: guildsEmbed.Build());
            IGuild guild;
            while (true)
            {
                var result = await Interactivity.NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
                var message = result.Value;
                if (message == null || message.Content == "cancel")
                {
                    await ReplyAsync("You have timed out or canceled");
                    return;
                }
                try
                {
                    guild = mutualGuilds[ushort.Parse(message.Content) - 1];
                    break;
                }
                catch
                {
                    await ReplyAsync("Invalid number, please reply again with a valid number or ``cancel``");
                }
            }

            List<Infraction> infractions = userRef.LoadInfractions(guild, false);
            if (infractions?.Count is null or 0)
            {
                string message = $"{userRef.Mention()} has no infractions";
                if (userRef.User == null) message += " or doesn't exist";
                await ReplyAsync(message);
                return;
            }
            userRef = userRef with { GuildUser = await guild.GetUserAsync(userRef.ID) };
            await ReplyAsync($"Here are {userRef.Mention()}'s {((amount < infractions.Count) ? $"last {amount} out of " : "")}{"infraction".ToQuantity(infractions.Count)}",
                embed: infractions.GetEmbed(userRef, amount: amount));
        }


        [Command("warns")]
        [Summary("Views a user's infractions.")]
        [CanWarn]
        [Alias("infractions", "warnings")]
        public async Task CheckUserWarnsAsync(UserRef userRef = null, int amount = 5)
        {
            userRef ??= new UserRef(Context.User as IGuildUser);
            List<Infraction> infractions = userRef.LoadInfractions(Context.Guild, false);
            if (infractions?.Count is null or 0)
            {
                await ReplyAsync($"{userRef.Name()} has no infractions");
                return;
            }
            await ReplyAsync(embed: infractions.GetEmbed(userRef, amount: amount, showLinks: true));
        }

        [Command("removewarn")]
        [Summary("Removes a warn from a user.")]
        [Alias("warnremove", "removewarning")]
        [HasAdmin()]
        public async Task RemoveWarnAsync([RequireHierarchy] UserRef userRef, int index)
        {
            List<Infraction> infractions = userRef.LoadInfractions(Context.Guild, false);
            if (infractions?.Count is null or 0)
            {
                await ReplyAsync("Infractions are null");
                return;
            }
            if (infractions.Count < index || index <= 0)
            {
                await ReplyAsync("Invalid infraction number");
                return;
            }
            string reason = infractions[index - 1].Reason;
            infractions.RemoveAt(index - 1);

            userRef.SaveInfractions(infractions, Context.Guild);
            await userRef.User?.TryNotify($"Your {index.Ordinalize()} warning in {Context.Guild.Name} discord for {reason} has been removed");
            await ReplyAsync("Removed " + userRef.Mention() + "'s warning for " + reason);
        }

        [Command("kickwarn")]
        [Summary("Kicks a user, and warns them with an option reason.")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn([RequireHierarchy] SocketGuildUser user, [Remainder] string reason = "Unspecified")
        {
            await user.Warn(1, reason, Context.Channel as ITextChannel, "Discord");
            await DiscordLogging.LogWarn(Context.Guild, Context.Message.Author, user.Id, reason, Context.Message.GetJumpUrl(), "kick");

            _ = user.Notify("kicked", reason, Context.Guild, Context.Message.Author);
            await user.KickAsync(reason);
            Context.Message.DeleteOrRespond($"{user.Mention} has been kicked for {reason} ", Context.Guild);
        }

        [Command("kickwarn")]
        [Summary("Kicks a user, and warns them with a specific size along with an option reason.")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn([RequireHierarchy] SocketGuildUser user, float size, [Remainder] string reason = "Unspecified")
        {
            await user.Warn(size, reason, Context.Channel as ITextChannel, "Discord");
            await DiscordLogging.LogWarn(Context.Guild, Context.Message.Author, user.Id, reason, Context.Message.GetJumpUrl(), "kick");

            _ = user.Notify("kicked", reason, Context.Guild, Context.Message.Author);
            await user.KickAsync(reason);
            Context.Message.DeleteOrRespond($"{user.Mention} has been kicked for {reason} ", Context.Guild);
        }

        [Command("tempban")]
        [Summary("Temporarily bans a user.")]
        [Alias("tban", "temp-ban")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempBanUser([RequireHierarchy] UserRef userRef, TimeSpan time, [Remainder] string reason)
        {
            if (time.TotalMinutes < 1)
            {
                await ReplyAsync("Can't temp-ban for less than a minute");
                return;
            }
            if (!(Context.Message.Author as IGuildUser).HasAdmin())
            {
                ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
                if (settings?.maxTempAction != null && time > settings.maxTempAction)
                {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            TempAct oldAct = actions.tempBans.FirstOrDefault(tempMute => tempMute.User == userRef.ID);
            if (oldAct != null)
            {
                if (!(Context.Message.Author as IGuildUser).HasAdmin() && (oldAct.Length - (DateTime.UtcNow - oldAct.DateBanned)) >= time)
                {
                    await ReplyAsync($"{Context.User.Mention} please contact your admin(s) in order to shorten length of a punishment");
                    return;
                }
                var text = $"{userRef.Name(true)} is already temp-banned for {oldAct.Length.LimitedHumanize()} ({(oldAct.Length - (DateTime.UtcNow - oldAct.DateBanned)).LimitedHumanize()} left), are you sure you want to change the length?";
                var request = new ConfirmationBuilder()
                    .WithContent(new PageBuilder().WithText(text))
                    .Build();
                var result = await Interactivity.SendConfirmationAsync(request, Context.Channel, TimeSpan.FromMinutes(2));
                if (result.Value)
                {
                    actions.tempBans.Remove(oldAct);
                    actions.SaveToFile();
                }
                else
                {
                    await ReplyAsync("Command canceled");
                    return;
                }
            }
            await userRef.TempBan(time, reason, Context, actions);
            Context.Message.DeleteOrRespond($"Temporarily banned {userRef.Mention()} for {time.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("tempbanwarn")]
        [Summary("Temporarily bans a user, and warns them with an option reason.")]
        [Alias("tbanwarn", "temp-banwarn", "tempbanandwarn", "tbw")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempBanWarnUser([RequireHierarchy] UserRef userRef, TimeSpan time, [Remainder] string reason)
        {
            if (time.TotalMinutes < 1)
            {
                await ReplyAsync("Can't temp-ban for less than a minute");
                return;
            }
            if (!(Context.Message.Author as IGuildUser).HasAdmin())
            {
                ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
                if (settings?.maxTempAction != null && time > settings.maxTempAction)
                {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            await userRef.Warn(1, reason, Context.Channel as ITextChannel, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempBans.Any(tempBan => tempBan.User == userRef.ID))
            {
                Context.Message.DeleteOrRespond($"{userRef.Name()} is already temp-banned (the warn did go through)", Context.Guild);
                return;
            }
            await userRef.TempBan(time, reason, Context, actions);
            Context.Message.DeleteOrRespond($"Temporarily banned {userRef.Mention()} for {time.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("tempbanwarn")]
        [Alias("tbanwarn", "temp-banwarn", "tempbanwarn", "warntempban", "tbw")]
        [Summary("Temporarily bans a user, and warns them with a specific size along with an option reason.")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempBanWarnUser([RequireHierarchy] UserRef userRef, TimeSpan time, float size, [Remainder] string reason)
        {
            if (time.TotalMinutes < 1)
            {
                await ReplyAsync("Can't temp-ban for less than a minute");
                return;
            }
            if (!(Context.Message.Author as IGuildUser).HasAdmin())
            {
                ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
                if (settings?.maxTempAction != null && time > settings.maxTempAction)
                {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            await userRef.Warn(size, reason, Context.Channel as ITextChannel, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempBans.Any(tempBan => tempBan.User == userRef.ID))
            {
                await ReplyAsync($"{userRef.Name()} is already temp-banned (the warn did go through)");
                return;
            }
            await userRef.TempBan(time, reason, Context, actions);
            Context.Message.DeleteOrRespond($"Temporarily banned {userRef.Mention()} for {time.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("tempmute", RunMode = RunMode.Async)]
        [Summary("Temporarily mutes a user in text channels.")]
        [Alias("tmute", "temp-mute")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempMuteUser([RequireHierarchy] UserRef userRef, TimeSpan time, [Remainder] string reason)
        {
            if (time.TotalMinutes < 1)
            {
                await ReplyAsync("Can't temp-mute for less than a minute");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (!(Context.Message.Author as IGuildUser).HasAdmin())
            {
                if (settings?.maxTempAction != null && time > settings.maxTempAction)
                {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            if (settings == null || settings.mutedRole == 0 || Context.Guild.GetRole(settings.mutedRole) == null)
            {
                await ReplyAsync("Muted role is null or invalid");
                return;
            }
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            TempAct oldAct = actions.tempMutes.FirstOrDefault(tempMute => tempMute.User == userRef.ID);
            if (oldAct != null)
            {
                if (!(Context.Message.Author as IGuildUser).HasAdmin() && (oldAct.Length - (DateTime.UtcNow - oldAct.DateBanned)) >= time)
                {
                    await ReplyAsync($"{Context.User.Mention} please contact your admin(s) in order to shorten length of a punishment");
                    return;
                }
                string text = $"{userRef.Name()} is already temp-muted for {oldAct.Length.LimitedHumanize()} ({(oldAct.Length - (DateTime.UtcNow - oldAct.DateBanned)).LimitedHumanize()} left), are you sure you want to change the length?";
                var request = new ConfirmationBuilder()
                    .WithContent(new PageBuilder().WithText(text))
                    .Build();
                var result = await Interactivity.SendConfirmationAsync(request, Context.Channel, TimeSpan.FromMinutes(2));
                if (result.Value)
                {
                    actions.tempMutes.Remove(oldAct);
                    actions.SaveToFile();
                }
                else
                {
                    await ReplyAsync("Command canceled");
                    return;
                }
            }

            await userRef.TempMute(time, reason, Context, settings, actions);
            Context.Message.DeleteOrRespond($"Temporarily muted {userRef.Mention()} for {time.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("tempmutewarn")]
        [Summary("Temporarily mutes a user in text channels, and warns them with an option reason.")]
        [Alias("tmutewarn", "temp-mutewarn", "warntmute", "tempmuteandwarn", "tmw")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempMuteWarnUser([RequireHierarchy] UserRef userRef, TimeSpan time, [Remainder] string reason)
        {
            if (time.TotalMinutes < 1)
            {
                await ReplyAsync("Can't temp-mute for less than a minute");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (!(Context.Message.Author as IGuildUser).HasAdmin())
            {
                if (settings?.maxTempAction != null && time > settings.maxTempAction)
                {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            if (settings == null || settings.mutedRole == 0 || Context.Guild.GetRole(settings.mutedRole) == null)
            {
                await ReplyAsync("Muted role is null or invalid");
                return;
            }
            await userRef.Warn(1, reason, Context.Channel as ITextChannel, Context.Message.GetJumpUrl());
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempMutes.Any(tempMute => tempMute.User == userRef.ID))
            {
                await ReplyAsync($"{userRef.Name()} is already temp-muted, (the warn did go through)");
                return;
            }

            await userRef.TempMute(time, reason, Context, settings, actions);
            Context.Message.DeleteOrRespond($"Temporarily muted {userRef.Mention()} for {time.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("tempmutewarn")]
        [Summary("Temporarily mutes a user in text channels, and warns them with a specific size along with an option reason.")]
        [Alias("tmutewarn", "temp-mutewarn", "warntmute", "tempmuteandwarn", "tmw")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempMuteWarnUser([RequireHierarchy] UserRef userRef, TimeSpan time, float size, [Remainder] string reason)
        {
            if (time.TotalMinutes < 1)
            {
                await ReplyAsync("Can't temp-mute for less than a minute");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (!(Context.Message.Author as IGuildUser).HasAdmin())
            {
                if (settings?.maxTempAction != null && time > settings.maxTempAction)
                {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            if (settings == null || settings.mutedRole == 0 || Context.Guild.GetRole(settings.mutedRole) == null)
            {
                await ReplyAsync("Muted role is null or invalid");
                return;
            }
            await userRef.Warn(size, reason, Context.Channel as ITextChannel, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempMutes.Any(tempMute => tempMute.User == userRef.ID))
            {
                await ReplyAsync($"{userRef.Name()} is already temp-muted, (the warn did go through)");
                return;
            }

            await userRef.TempMute(time, reason, Context, settings, actions);
            Context.Message.DeleteOrRespond($"Temporarily muted {userRef.Mention()} for {time.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("ban", RunMode = RunMode.Async)]
        [Summary("Bans a user with an option reason.")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Ban([RequireHierarchy] UserRef userRef, [Remainder] string reason)
        {
            if (reason.Split(' ').First().ToTime() != null)
            {
                var query = "Are you sure you don't mean to use !tempban?";
                var request = new ConfirmationBuilder()
                    .WithContent(new PageBuilder().WithText(query))
                    .Build();
                var result = await Interactivity.SendConfirmationAsync(request, Context.Channel, TimeSpan.FromMinutes(1));
                if (result.Value)
                {
                    await ReplyAsync("Command Canceled");
                    return;
                }
            }

            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(false);
            if (actions?.tempBans?.Any(tempBan => tempBan.User == userRef.ID) ?? false)
            {
                var query = "User is already tempbanned, are you use you want to ban?";
                var request = new ConfirmationBuilder()
                    .WithContent(new PageBuilder().WithText(query))
                    .Build();
                var result = await Interactivity.SendConfirmationAsync(request, Context.Channel, TimeSpan.FromMinutes(2));
                if (result.Value)
                {
                    await ReplyAsync("Command Canceled");
                    return;
                }
                actions.tempBans.Remove(actions.tempBans.First(tempban => tempban.User == userRef.ID));
            }
            else if ((await Context.Guild.GetBansAsync()).Any(ban => ban.User.Id == userRef.ID))
            {
                await ReplyAsync("User has already been banned permanently");
                return;
            }
            await userRef.User?.TryNotify($"You have been perm banned in the {Context.Guild.Name} discord for {reason}");
            await Context.Guild.AddBanAsync(userRef.ID, reason: reason);
            DiscordLogging.LogTempAct(Context.Guild, Context.Message.Author, userRef, "Bann", reason, Context.Message.GetJumpUrl(), TimeSpan.Zero);
            Context.Message.DeleteOrRespond($"{userRef.Name(true)} has been banned for {reason}", Context.Guild);
        }

        [Command("delete")]
        [Summary("Clear a specific number of messages between 0-300.")]
        [Alias("clean", "clear", "deletemany", "purge")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task DeleteMany(uint number, UserRef user = null)
        {
            if (number == 0 || number > 300)
            {
                await ReplyAsync("Invalid number");
                return;
            }
            if (user?.GuildUser != null && user.GuildUser.GetHierarchy() >= ((IGuildUser)Context.User).GetHierarchy())
            {
                await ReplyAsync("Can't target deleted messages belonging to people with higher hierarchy");
                return;
            }

            uint searchedMessages = number;
            List<IMessage> messages = null;
            if (user == null) messages = await Context.Channel.GetMessagesAsync((int)number).Flatten().ToListAsync();
            else
            {
                searchedMessages = 100;
                messages = await Context.Channel.GetMessagesAsync(100).Flatten().ToListAsync();
                for (int i = 0; i < 3; i++)
                {
                    var lastMessage = messages.Last();
                    if (lastMessage.GetTimeAgo() > TimeSpan.FromDays(14)) break;
                    messages.RemoveAll(message => message.Author.Id != user.ID);
                    if (messages.Count >= number)
                    {
                        break;
                    }
                    searchedMessages += 100;
                    messages.Concat(await Context.Channel.GetMessagesAsync(lastMessage, Direction.After, 100).Flatten().ToListAsync());
                }
                if (messages.Count > 0)
                {
                    messages.RemoveAll(message => message.Author.Id != user.ID);
                    if (messages.Count > number) messages.RemoveRange((int)number, messages.Count - (int)number);
                }
            }

            bool timeRanOut = false;
            if (messages.Count > 0)
            {
                if (messages.Last().GetTimeAgo() > TimeSpan.FromDays(14))
                {
                    timeRanOut = true;
                    messages.RemoveAll(message => message.GetTimeAgo() > TimeSpan.FromDays(14));
                }
                await ExceptionLogging.AssertAsync(messages.Count <= number);

                //No need to delete messages or log if no actual messages deleted
                await (Context.Channel as ITextChannel).DeleteMessagesAsync(messages);
                LogSettings logSettings = Context.Guild.LoadFromFile<LogSettings>(false);
                if (Context.Guild.TryGetChannel(logSettings?.logChannel ?? 0, out IGuildChannel logChannel))
                {
                    var embed = new EmbedBuilder();
                    embed.WithColor(Color.DarkRed);
                    embed.WithCurrentTimestamp();
                    embed.WithAuthor(Context.User);
                    embed.WithTitle("Mass message deletion");
                    embed.AddField("Messages searched", $"{searchedMessages} messages", true);
                    embed.AddField("Messages deleted", $"{messages.Count} messages", true);
                    embed.AddField("Channel", ((ITextChannel)Context.Channel).Mention, true);
                    await ((ISocketMessageChannel)logChannel).SendMessageAsync(embed: embed.Build());
                }
            }
            string extra = "";
            if (searchedMessages != messages.Count) extra = $" out of {searchedMessages} searched messages";
            if (timeRanOut) extra = " (note, due to ratelimits and discord limitations, only messages in the last two weeks can be mass deleted)";
            Context.Message.DeleteOrRespond($"{Context.User.Mention} deleted {messages.Count} messages{extra}", Context.Guild);
        }
    }
}