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
/// and including an <see cref="Interactivity"/> property as well as useful methods.
/// </summary>
public class InteractiveModule : ModuleBase<ICommandContext>
{
    /// <summary>
    /// This constructor is required for DI to work in both test environment and release without
    /// mocking of <seealso cref="Discord.WebSocket.BaseSocketClient"/>
    /// </summary>
    public InteractiveModule(IServiceProvider service)
    {
        Interactivity = (InteractiveService)service.GetService(typeof(InteractiveService))!;
    }

    protected InteractiveService Interactivity { get; }

    /// <summary>
    /// Adds confirm and deny buttons to a message and then waits for the author to react.
    /// </summary>
    /// <param name="message">The query to ask the user.</param>
    /// <param name="timeout">The time to wait before the methods returns a timeout result. Defaults to 2 minutes.</param>
    /// <returns>Returns true if didn't time out and confirm is selected.</returns>
    public async Task<bool> TryConfirmation(string message, TimeSpan? timeout = null)
    {
        var page = new PageBuilder()
                   .WithColor(Color.Blue)
                   .WithDescription(message);
        var selection = new ButtonSelectionBuilder<string>()
            .AddUser(Context.User)
            .WithSelectionPage(page)
            .AddOption(new ButtonOption<string>("Confirm", ButtonStyle.Danger))
            .AddOption(new ButtonOption<string>("Cancel", ButtonStyle.Secondary))
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnSuccess(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithStringConverter(x => x.Option)
            .Build();

        var result = await Interactivity.SendSelectionAsync(selection, Context.Channel, timeout ?? TimeSpan.FromMinutes(2));
        return result.IsSuccess && result.Value!.Option == "Confirm";
    }

    /// <summary>
    /// Confirms a user isn't already under a specific TempAct and displays a confirmation to overwrite if they are.
    /// </summary>
    /// <param name="actions">The list of TempActions to be searched.</param>
    /// <param name="newAction">The TempAction to be compared with the old action.</param>
    /// <param name="user">If an <see cref="IUser"/> is included show on embed.</param>
    /// <typeparam name="TAct">The type of act which should be able to be inferred and just serve for type safety.</typeparam>
    /// <returns>A Tuple so so that the result can be used and action can be removed.</returns>
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
                   .WithColor(Color.Blue)
                   .AddField($"Current Action:", oldInfo)
                   .AddField("Overwrite With:", $"{newAction.Type} starting {MentionTime(newAction.Start, 'R')} and ending {MentionTime(newAction.EndTime, 'R')}.")
                   .WithTitle($"Are you sure you want to overwrite existing {newAction.Type}?");

        if (user.User != null)
            page.WithAuthor(user.User);

        var selection = new ButtonSelectionBuilder<string>()
                        .AddUser(Context.User)
                        .WithSelectionPage(page)
                        .AddOption(new ButtonOption<string>("Confirm", ButtonStyle.Danger))
                        .AddOption(new ButtonOption<string>("Cancel", ButtonStyle.Secondary))
                        .WithActionOnCancellation(ActionOnStop.DisableInput)
                        .WithActionOnSuccess(ActionOnStop.DisableInput)
                        .WithActionOnTimeout(ActionOnStop.DisableInput)
                        .WithStringConverter(x => x.Option)
                        .Build();

        var result = await Interactivity.SendSelectionAsync(selection, Context.Channel, TimeSpan.FromMinutes(1));

        if (result.IsSuccess && result.Value!.Option == "Confirm")
            return (null, oldAction);
        return (CommandResult.FromError("Command has been canceled."), null);
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
