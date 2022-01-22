﻿using System;
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

    public async Task<RuntimeResult?> ConfirmNoTempAct(IReadOnlyList<TempAction>? actions, ulong userID, PageBuilder page)
    {
        if (actions?.Count is null or 0 || actions.Any(action => action.UserId == userID) == false)
            return null;

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
        return result.IsCanceled ? CommandResult.FromError("Command has been canceled.") : null;
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
}