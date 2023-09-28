using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Moderation;
using BotCatMaxy.Services.Logging;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Components.CommandHandling;
using BotCatMaxy.Components.Interactivity;

namespace BotCatMaxy
{
    [Name("Moderation")]
    public class ModerationCommands : InteractiveModule
    {
        public ModerationCommands(IServiceProvider service) : base(service) { }

        public async Task<RuntimeResult> ExecuteWarnAsync(UserRef userRef, float size, string reason)
        {
            IUserMessage logMessage = await DiscordLogging.LogWarn(Context.Guild, Context.Message.Author, userRef.ID, reason, Context.Message.GetJumpUrl());
            WarnResult result = await userRef.Warn(size, reason, Context.Channel as ITextChannel, logMessage?.GetJumpUrl());

            if (result.success)
            {
                string modifier = (size != 1) ? $"(size of `{size}x`) " : "";
                return CommandResult.FromSuccess($"{userRef.Mention()} has been given their `{result.warnsAmount.Suffix()}` warning {modifier}because of `{reason}`.");
            }

            if (logMessage != null)
            {
                DiscordLogging.deletedMessagesCache.Enqueue(logMessage.Id);
                await logMessage.DeleteAsync();
            }
            return CommandResult.FromError(result.description.Truncate(1500));
        }

        [Command("warn")]
        [Summary("Warn a user with a reason.")]
        [CanWarn()]
        public Task<RuntimeResult> WarnUserAsync([RequireHierarchy] UserRef userRef, [Remainder] string reason)
            => ExecuteWarnAsync(userRef, 1, reason);

        [Command("warn")]
        [Summary("Warn a user with a specific size, along with a reason.")]
        [CanWarn()]
        public Task WarnUserWithSizeAsync([RequireHierarchy] UserRef userRef, float size, [Remainder] string reason)
            => ExecuteWarnAsync(userRef, size, reason);

        #nullable enable
        [Command("dmwarns", RunMode = RunMode.Async)]
        [Summary("Views a user's infractions.")]
        [RequireContext(ContextType.DM, ErrorMessage = "This command now only works in the bot's DMs")]
        [Alias("dminfractions", "dmwarnings", "warns", "infractions", "warnings")]
        public async Task<RuntimeResult> DMUserWarnsAsync(UserRef? userRef = null, int amount = 50)
        {
            if (amount < 1)
                return CommandResult.FromError("Why would you want to see that many infractions?");

            var guild = await QueryMutualGuild();
            if (guild == null)
                return CommandResult.FromError("You have timed out or canceled.");

            userRef ??= new UserRef(Context.Message.Author);
            List<Infraction> infractions = userRef.LoadInfractions(guild, false);
            if (infractions?.Count is null or 0)
            {
                var message = $"{userRef.Mention()} has no infractions";
                if (userRef.User == null)
                    message += " or doesn't exist";
                return CommandResult.FromSuccess(message);
            }

            userRef = userRef with { GuildUser = await guild.GetUserAsync(userRef.ID) };
            var embed = infractions.GetEmbed(userRef, guild, amount: amount);
            return CommandResult.FromSuccess($"Here are {userRef.Mention()}'s {((amount < infractions.Count) ? $"last {amount} out of " : "")}{"infraction".ToQuantity(infractions.Count)}",
                embed: embed);
        }
        #nullable disable

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
            await ReplyAsync(embed: infractions.GetEmbed(userRef, Context.Guild, amount: amount, showLinks: true));
        }

        [Command("removewarn")]
        [Summary("Removes a warn from a user.")]
        [Alias("warnremove", "removewarning")]
        [HasAdmin()]
        public async Task<RuntimeResult> RemoveWarnAsync([RequireHierarchy] UserRef userRef, int index)
        {
            List<Infraction> infractions = userRef.LoadInfractions(Context.Guild, false);
            if (infractions?.Count is null or 0)
            {
                return CommandResult.FromError("Infractions are null");
            }
            if (infractions.Count < index || index <= 0)
            {
                return CommandResult.FromError("Invalid infraction number");
            }
            string reason = infractions[index - 1].Reason;
            infractions.RemoveAt(index - 1);

            userRef.SaveInfractions(infractions, Context.Guild);
            if (userRef.User != null) //Can't use null propagation on awaited tasks since it would be awaiting null
                await userRef.User.TryNotify($"Your {index.Ordinalize()} warning in {Context.Guild.Name} discord for {reason} has been removed");
            return CommandResult.FromSuccess($"Removed {userRef.Mention()}'s warning for {reason}");
        }

        [Command("kickwarn")]
        [Summary("Kicks a user, and warns them with an optional reason.")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn([RequireHierarchy] SocketGuildUser user, [Remainder] string reason = "Unspecified")
        {
            await user.Warn(1, reason, Context.Channel as ITextChannel, "Discord");
            await DiscordLogging.LogWarn(Context.Guild, Context.Message.Author, user.Id, reason, Context.Message.GetJumpUrl(), "kick");

            _ = user.Notify("kicked", reason, Context.Guild, Context.Message.Author);
            await user.KickAsync(reason);
            Context.Message.DeleteOrRespond($"{user.Mention} has been kicked for {reason}", Context.Guild);
        }

        [Command("kickwarn")]
        [Summary("Kicks a user, and warns them with a specific size along with an optional reason.")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn([RequireHierarchy] SocketGuildUser user, float size, [Remainder] string reason = "Unspecified")
        {
            await user.Warn(size, reason, Context.Channel as ITextChannel, "Discord");
            await DiscordLogging.LogWarn(Context.Guild, Context.Message.Author, user.Id, reason, Context.Message.GetJumpUrl(), "kick");

            _ = user.Notify("kicked", reason, Context.Guild, Context.Message.Author);
            await user.KickAsync(reason);
            Context.Message.DeleteOrRespond($"{user.Mention} has been kicked for {reason}", Context.Guild);
        }

        [Command("ban", RunMode = RunMode.Async)]
        [Summary("Bans a user with a reason.")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task<RuntimeResult> Ban([RequireHierarchy] UserRef userRef, [Remainder] string reason)
        {
            if (reason.Split(' ').First().ToTime() != null && await TryConfirmation("Are you sure you don't mean to use `!tempban`?") == false)
            {
                return CommandResult.FromError("Command canceled.");
            }

            var actions = Context.Guild.LoadFromFile<TempActionList>(false);
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);

            if (actions?.tempBans?.Any(tempBan => tempBan.UserId == userRef.ID) ?? false)
            {
                if (await TryConfirmation("User is already TempBanned, are you sure you want to ban?") == false)
                    return CommandResult.FromError("Command canceled.");
                actions.tempBans.Remove(actions.tempBans.First(tempBan => tempBan.UserId == userRef.ID));
            }
            else if (await Context.Guild.GetBanAsync(userRef.ID) != null)
            {
                return CommandResult.FromError("User has already been banned permanently.");
            }

            if (userRef.User != null)
                await userRef.User.Notify($"permanently banned", reason, Context.Guild, Context.Message.Author, appealLink: settings.appealLink);
            await Context.Guild.AddBanAsync(userRef.ID, reason: reason);
            await DiscordLogging.LogTempAct(Context.Guild, Context.Message.Author, userRef, "Bann", reason, Context.Message.GetJumpUrl(), TimeSpan.Zero);
            return CommandResult.FromSuccess($"{userRef.Name(true)} has been banned for `{reason}`.");
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
            if (timeRanOut) extra += " (reached limit due to ratelimits and Discord limitations, because only messages in the last two weeks can be mass deleted)";
            Context.Message.DeleteOrRespond($"{Context.User.Mention} deleted {messages.Count} messages{extra}", Context.Guild);
        }
    }
}