using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
#nullable enable
namespace BotCatMaxy.Components.CommandHandling;

public class CommandModule : InteractionModuleBase<IInteractionContext>
{
    // Insanity that this has to be made
    // Purposefully not allowing ephemeral because those are not real messages
    private async Task<IUserMessage> RespondWithMessageAsync(string message, Embed embed = null, MessageComponent component = null)
    {
        await RespondAsync(message, embed: embed, components: component);
        return await Context.Interaction.GetOriginalResponseAsync();
    }

    protected async Task<IUserMessage> DeferWithMessageAsync()
    {
        await DeferAsync();
        return await Context.Interaction.GetOriginalResponseAsync();
    }
    
    /// <summary>
    /// Confirms a user isn't already under a specific TempAct and displays a confirmation to overwrite if they are.
    /// </summary>
    /// <param name="actions">The list of TempActions to be searched.</param>
    /// <param name="newAction">The TempAction to be compared with the old action.</param>
    /// <param name="user">If an <see cref="IUser"/> is included show on embed.</param>
    /// <typeparam name="TAct">The type of act which should be able to be inferred and just serve for type safety.</typeparam>
    /// <returns>A Tuple so so that the result can be used and action can be removed.</returns>
    /*public async Task<(CommandResult result, TAct action)?> ConfirmNoTempAct<TAct>(IReadOnlyList<TAct>? actions, TAct newAction, UserRef user) where TAct : TempAction
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
        // var page = new PageBuilder()
        //            .WithColor(Color.Blue)
        //            .AddField($"Current Action:", oldInfo)
        //            .AddField("Overwrite With:", $"{newAction.Type} starting {MentionTime(newAction.Start, 'R')} and ending {MentionTime(newAction.EndTime, 'R')}.")
        //            .WithTitle($"Are you sure you want to overwrite existing {newAction.Type}?");
        //
        //
        // if (user.User != null)
        //     page.WithAuthor(user.User);
        //
        // var selection = new ButtonSelectionBuilder<string>()
        //                 .AddUser(Context.User)
        //                 .WithSelectionPage(page)
        //                 .AddOption(new ButtonOption<string>("Confirm", ButtonStyle.Danger))
        //                 .AddOption(new ButtonOption<string>("Cancel", ButtonStyle.Secondary))
        //                 .WithActionOnCancellation(ActionOnStop.DisableInput)
        //                 .WithActionOnSuccess(ActionOnStop.DisableInput)
        //                 .WithActionOnTimeout(ActionOnStop.DisableInput)
        //                 .WithStringConverter(x => x.Option)
        //                 .Build();
        //
        // var result = await Interactivity.SendSelectionAsync(selection, Context.Channel, TimeSpan.FromMinutes(1));
        //
        // if (result.IsSuccess && result.Value!.Option == "Confirm")
        //     return (null, oldAction);
        // return (CommandResult.FromError("Command has been canceled."), null);
    }*/

    /// <summary>
    /// Asks the user what server they want to be talking about if they are not currently in a server.
    /// </summary>
    /// <returns>An <see cref="IGuild"/> or <c>null</c> if canceled.</returns>
    protected async Task<IGuild?> QueryMutualGuild()
    {
        if (Context.Guild != null)
            return Context.Guild;

        var mutualGuilds = (await Context.User.GetMutualGuildsAsync(Context.Client)).ToImmutableArray();
        var menu = new SelectMenuBuilder()
            .WithPlaceholder("Select server in dropdown")
            // required menu
            .WithCustomId("guild-selection");
        for (var i = 0; i < mutualGuilds.Length; i++)
        {
            string name = mutualGuilds[i].Name.Truncate(30, "...");
            string id = mutualGuilds[i].Id.ToString();
            menu.AddOption($"{name}", id, $"{name} guild");
            //Could pad ID right, but honestly is it worth it?
        }

        var component = new ComponentBuilder()
            .WithSelectMenu(menu);
        var message = await RespondWithMessageAsync("Select server to view", component: component.Build());
        var interaction = await InteractionUtility.WaitForMessageComponentAsync(Context.Client as BaseSocketClient, message, TimeSpan.FromMinutes(5));
        var response = (interaction as SocketMessageComponent)?.Data?.Values?.First();
        if (response != null && ulong.TryParse(response, out ulong parsedId))
        {
            await interaction.DeferAsync(ephemeral: true);
            return await Context.Client.GetGuildAsync(parsedId);
        }

        return null;
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