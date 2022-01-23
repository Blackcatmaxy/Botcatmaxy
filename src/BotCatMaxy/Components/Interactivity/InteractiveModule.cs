using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.Interactive;

#nullable enable
namespace BotCatMaxy.Components.Interactivity;

/// <summary>
/// Wrapper around <see cref="ModuleBase{T}"/> with T as <see cref="ICommandContext"/>
/// and including an <see cref="Interactivity"/> property
/// </summary>
public class InteractiveModule : ModuleBase<ICommandContext>
{
    /// <summary>
    /// This constructor is required for DI to work in both test environment and release without
    /// mocking of <seealso cref="Discord.WebSocket.BaseSocketClient"/>
    /// </summary>
    public InteractiveModule(IServiceProvider service) : base()
    {
        Interactivity = (InteractiveService)service.GetService(typeof(InteractiveService))!;
    }

    protected InteractiveService Interactivity { get; }
    protected static readonly Emoji ConfirmEmoji = new("\U00002705");
    protected static readonly Emoji CancelEmoji = new("\U0000274c");

    /// <summary>
    /// Adds confirm and deny reactions to a message and then waits for the author to react.
    /// </summary>
    /// <param name="message">The message to check for reactions from</param>
    /// <param name="timeout">The time to wait before the methods returns a timeout result. Defaults to 2 minutes.</param>
    /// <returns>Returns true if didn't time out and confirm is selected</returns>
    public async Task<bool> TryConfirmation(IMessage message, TimeSpan? timeout = null)
    {
        await message.AddReactionAsync(CancelEmoji);
        await message.AddReactionAsync(ConfirmEmoji);
        var reaction = await Interactivity.NextReactionAsync(reaction
            => reaction.MessageId == message.Id && reaction.UserId == message.Author.Id, timeout: timeout ?? TimeSpan.FromMinutes(2));

        return (reaction.IsSuccess && Equals(reaction.Value.Emote, ConfirmEmoji));
    }

    public async Task<(CommandResult? result, TAct? action)?> ConfirmNoTempAct<TAct>(IReadOnlyList<TAct>? actions, TAct newAction, UserRef user) where TAct : TempAction
    {
        if (actions?.Count is null or 0 || actions.Any(action => action.UserId == user.ID) == false)
            return null;
        TAct? oldAction = null;
        foreach (var action in actions)
        {
            if (action.UserId == user.ID)
                oldAction = action;
        }

        if (oldAction == null)
            return null;
        string oldInfo =
            $"{oldAction.Type} for {oldAction.Length.LimitedHumanize()} for `{oldAction.Reason}` started {MentionTime(oldAction.Start, 'R')} and ending {MentionTime(oldAction.EndTime, 'R')}.";
        var page = new PageBuilder()
                   .AddField($"Current Action:", oldInfo)
                   .AddField("Overwrite With:", $"{newAction.Type} starting {MentionTime(newAction.Start, 'R')} and ending {MentionTime(newAction.EndTime, 'R')}.")
                   .WithTitle($"Are you sure you want to overwrite existing {newAction.Type}?");

        if (user.User != null)
            page.WithAuthor(user.User);

        var selection = new ButtonSelectionBuilder<string>()
                        .AddUser(Context.User)
                        .WithSelectionPage(page)
                        .AddOption(new ButtonOption<string>("Confirm", ButtonStyle.Success))
                        .AddOption(new ButtonOption<string>("Cancel", ButtonStyle.Danger))
                        .WithActionOnCancellation(ActionOnStop.DisableInput)
                        .WithActionOnSuccess(ActionOnStop.DisableInput)
                        .WithActionOnTimeout(ActionOnStop.DisableInput)
                        .WithStringConverter(x => x.Option)
                        .WithAllowCancel(true)
                        .Build();

        var result = await Interactivity.SendSelectionAsync(selection, Context.Channel, TimeSpan.FromMinutes(1));

        if (result.IsCanceled)
            return (CommandResult.FromError("Command has been canceled."), null);
        return (null, oldAction);
    }

    /// <summary>
    /// Asks the user what server they want to be talking about if they are not currently in a server.
    /// </summary>
    /// <returns>An <see cref="IGuild"/> or <c>null</c> if canceled.</returns>
    public async Task<IGuild?> QueryMutualGuild()
    {
        if (Context.Guild != null)
            return Context.Guild;

        var mutualGuilds = (await Context.Message.Author.GetMutualGuildsAsync(Context.Client)).ToImmutableArray();

        var guildsEmbed = new EmbedBuilder();
        guildsEmbed.WithTitle("Reply with the number corresponding to the server you're asking about:");

        for (var i = 0; i < mutualGuilds.Length; i++)
        {
            guildsEmbed.AddField($"[{i + 1}] {mutualGuilds[i].Name}", mutualGuilds[i].Id);
        }

        await Context.Channel.SendMessageAsync(embed: guildsEmbed.Build());
        while (true)
        {
            Task<InteractiveResult<SocketMessage?>> task = Interactivity.NextMessageAsync(msg => msg.Channel.Id == Context.Channel.Id, timeout: TimeSpan.FromMinutes(1));
            IMessage? message = (await task).Value;
            if (message?.Content is null or "cancel")
            {
                return null;
            }

            if (byte.TryParse(message.Content, out byte index) && index > 0)
                return mutualGuilds[index - 1];
            else
                await Context.Channel.SendMessageAsync("Invalid number, please reply again with a valid number or ``cancel``");
        }
    }

    /// <summary>
    /// Format a <see cref="DateTimeOffset"/> for Discord client rendering.
    /// </summary>
    /// <param name="offset">The <see cref="DateTimeOffset"/> to be formatted.</param>
    /// <param name="styleChar">The <a href="https://discord.com/developers/docs/reference#message-formatting-formats">character to set the style</a> on the client </param>
    public string MentionTime(DateTimeOffset offset, char? styleChar = null)
    {
        string style = (styleChar != null) ? $":{styleChar}" : "";
        return $"<t:{offset.ToUnixTimeSeconds()}{style}>";
    }
}