using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Moderation;
using BotCatMaxy.Services.Logging;
using Discord;
using Discord.WebSocket;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Components.CommandHandling;
using BotCatMaxy.Components.Interactivity;
using Discord.Commands;
using Discord.Interactions;
using ContextType = Discord.Commands.ContextType;
using IResult = Discord.Interactions.IResult;
using RunMode = Discord.Commands.RunMode;
using RuntimeResult = Discord.Interactions.RuntimeResult;
#nullable enable

namespace BotCatMaxy
{
    public class SlashModerationCommands : CommandModule
    {
        public async Task<RuntimeResult> ExecuteWarnAsync(IUser user, float size, string reason)
        {
            var message = await DeferWithMessageAsync();
            IUserMessage logMessage = await DiscordLogging.LogWarn(Context.Guild, Context.User, user.Id, reason, message.GetJumpUrl());
            WarnResult result = await user.Id.Warn(size, reason, Context.Channel as ITextChannel, user, logMessage?.GetJumpUrl());

            if (result.success)
            {
                string modifier = (size != 1) ? $"(size of `{size}x`) " : "";
                return CommandResult.FromSuccess($"{user.Mention} has been given their `{result.warnsAmount.Suffix()}` warning {modifier}because of `{reason}`.");
            }

            // did not succeed so we should delete log indicating successful warning
            // no need to log our hiding of a failed warn
            if (logMessage != null)
            {
                DiscordLogging.deletedMessagesCache.Enqueue(logMessage.Id);
                await logMessage.DeleteAsync();
            }
            return CommandResult.FromError(result.description.Truncate(1500));
        }

        [SlashCommand("warn", "warn a user")]
        [Discord.Interactions.CanWarn()]
        public Task WarnUserWithSizeAsync([Discord.Interactions.RequireHierarchy] IUser user, string reason, float size = 1)
            => ExecuteWarnAsync(user, size, reason);

        [SlashCommand("displaywarns", "show a user's warns")]
        [Discord.Interactions.CanWarn]
        public async Task<RuntimeResult> DisplayUserWarnsAsync(IUser? user = null, int amount = 5)
        {
            if (amount < 1)
                return CommandResult.FromError("Why would you want to see that many infractions?");

            user ??= Context.User;
            List<Infraction> infractions = user.Id.LoadInfractions(Context.Guild, false);
            if (infractions?.Count is null or 0)
                return CommandResult.FromSuccess($"{user.Mention} has no infractions.");

            var embed = infractions.GetEmbed(user, Context.Guild, amount: amount);
            return CommandResult.FromSuccess($"Here are {user.Mention}'s {((amount < infractions.Count) ? $"last {amount} out of " : "")}{"infraction".ToQuantity(infractions.Count)}",
                embed: embed);
        }

        [SlashCommand("warns", "view a user's warns")]
        public async Task CheckUserWarnsAsync(IUser? user = null, int amount = 5)
        {
            user ??= Context.User;
            var guild = await QueryMutualGuild();
            List<Infraction> infractions = user.Id.LoadInfractions(guild, false);
            if (infractions?.Count is null or 0)
            {
                await FollowupAsync($"{user.Username} has no infractions", ephemeral: true);
                return;
            }
            await FollowupAsync(embed: infractions.GetEmbed(user, guild, amount: amount, showLinks: true), ephemeral: true);
        }

        [SlashCommand("removewarn", "remove a user's warn")]
        [HasAdmin()]
        public async Task<RuntimeResult> RemoveWarnAsync([Discord.Interactions.RequireHierarchy] IUser user, int index)
        {
            List<Infraction> infractions = user.Id.LoadInfractions(Context.Guild, false);
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

            user.Id.SaveInfractions(Context.Guild, infractions);
            // if (user != null) //Can't use null propagation on awaited tasks since it would be awaiting null
                // await user.TryNotify($"Your {index.Ordinalize()} warning in {Context.Guild.Name} discord for {reason} has been removed");
            return CommandResult.FromSuccess($"Removed {user.Mention}'s warning for {reason}");
        }

        [SlashCommand("kickwarn", "kick a user and warn them with an optional reason")]
        [Discord.Commands.Summary("Kicks a user, and warns them with an optional reason.")]
        [Discord.Commands.RequireContext(ContextType.Guild)]
        [Discord.Commands.RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn([Discord.Interactions.RequireHierarchy] SocketGuildUser user, string reason = "Unspecified")
        {
            var invocation = await Context.Interaction.GetOriginalResponseAsync();
            await user.Warn(1, reason, Context.Channel as ITextChannel, "Discord");
            await DiscordLogging.LogWarn(Context.Guild, Context.User, user.Id, reason, invocation.GetJumpUrl(), "kick");

            _ = user.Notify("kicked", reason, Context.Guild, Context.User);
            await user.KickAsync(reason);
            await RespondAsync($"{user.Mention} has been kicked for {reason}");
        }

        // [SlashCommand("kickwarn", "kick a user, and warn them with a specific size along with an optional reason")]
        // [Discord.Commands.Summary("Kicks a user, and warns them with a specific size along with an optional reason.")]
        // [Alias("warnkick", "warnandkick", "kickandwarn")]
        // [Discord.Commands.RequireContext(ContextType.Guild)]
        // [Discord.Commands.RequireUserPermission(GuildPermission.KickMembers)]
        // public async Task KickAndWarn([RequireHierarchy] SocketGuildUser user, float size, [Remainder] string reason = "Unspecified")
        // {
        //     var invocation = await Context.Interaction.GetOriginalResponseAsync();
        //     await user.Warn(size, reason, Context.Channel as ITextChannel, "Discord");
        //     await DiscordLogging.LogWarn(Context.Guild, Context.User, user.Id, reason, invocation.GetJumpUrl(), "kick");
        //
        //     _ = user.Notify("kicked", reason, Context.Guild, Context.User);
        //     await user.KickAsync(reason);
        //     await RespondAsync($"{user.Mention} has been kicked for {reason}");
        // }

        // [SlashCommand("ban", RunMode = RunMode.Async)]
        // [Discord.Commands.Summary("Bans a user with a reason.")]
        // [Discord.Commands.RequireContext(ContextType.Guild)]
        // [Discord.Commands.RequireBotPermission(GuildPermission.BanMembers)]
        // [Discord.Commands.RequireUserPermission(GuildPermission.BanMembers)]
        // public async Task<RuntimeResult> Ban([RequireHierarchy] IUser user, [Remainder] string reason)
        // {
        //     if (reason.Split(' ').First().ToTime() != null && await TryConfirmation("Are you sure you don't mean to use `!tempban`?") == false)
        //     {
        //         return CommandResult.FromError("Command canceled.");
        //     }
        //
        //     var actions = Context.Guild.LoadFromFile<TempActionList>(false);
        //     ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
        //
        //     if (actions?.tempBans?.Any(tempBan => tempBan.UserId == user.ID) ?? false)
        //     {
        //         if (await TryConfirmation("User is already TempBanned, are you sure you want to ban?") == false)
        //             return CommandResult.FromError("Command canceled.");
        //         actions.tempBans.Remove(actions.tempBans.First(tempBan => tempBan.UserId == user.ID));
        //     }
        //     else if (await Context.Guild.GetBanAsync(user.ID) != null)
        //     {
        //         return CommandResult.FromError("User has already been banned permanently.");
        //     }
        //
        //     if (user.User != null)
        //         await user.User.Notify($"permanently banned", reason, Context.Guild, Context.Message.Author, appealLink: settings.appealLink);
        //     await Context.Guild.AddBanAsync(user.ID, reason: reason);
        //     await DiscordLogging.LogTempAct(Context.Guild, Context.Message.Author, user, "Bann", reason, Context.Message.GetJumpUrl(), TimeSpan.Zero);
        //     return CommandResult.FromSuccess($"{user.Name(true)} has been banned for `{reason}`.");
        // }

        [SlashCommand("delete", "clear a specific number of messages")]
        [Discord.Interactions.RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task DeleteMany(uint number, IUser user = null)
        {
            if (number == 0 || number > 300)
            {
                await ReplyAsync("Invalid number");
                return;
            }
            
            if (user is IGuildUser guildUser && guildUser.GetHierarchy() >= ((IGuildUser)Context.User).GetHierarchy())
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
                    messages.RemoveAll(message => message.Author.Id != user.Id);
                    if (messages.Count >= number)
                    {
                        break;
                    }
                    searchedMessages += 100;
                    messages.Concat(await Context.Channel.GetMessagesAsync(lastMessage, Direction.After, 100).Flatten().ToListAsync());
                }
                if (messages.Count > 0)
                {
                    messages.RemoveAll(message => message.Author.Id != user.Id);
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
            await RespondAsync($"{Context.User.Mention} deleted {messages.Count} messages{extra}");
        }
    }
}