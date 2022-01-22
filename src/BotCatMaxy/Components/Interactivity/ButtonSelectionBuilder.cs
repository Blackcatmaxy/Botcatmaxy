using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Fergun.Interactive;
using Fergun.Interactive.Selection;

namespace BotCatMaxy.Components.Interactivity;

public class ButtonSelectionBuilder<T> : BaseSelectionBuilder<ButtonSelection<T>, ButtonOption<T>, ButtonSelectionBuilder<T>>
{
    // Since this selection specifically created for buttons, it makes sense to make this option the default.
    public override InputType InputType => InputType.Buttons;

    // We must override the Build method
    public override ButtonSelection<T> Build() => new(EmoteConverter, StringConverter,
        EqualityComparer, AllowCancel, SelectionPage?.Build(), Users?.ToArray(), Options?.ToArray(),
        CanceledPage?.Build(), TimeoutPage?.Build(), SuccessPage?.Build(), Deletion, InputType,
        ActionOnCancellation, ActionOnTimeout, ActionOnSuccess);
}

// Custom selection where you can override the default button style/color
public class ButtonSelection<T> : BaseSelection<ButtonOption<T>>
{
    public ButtonSelection(Func<ButtonOption<T>, IEmote> emoteConverter, Func<ButtonOption<T>, string> stringConverter,
        IEqualityComparer<ButtonOption<T>> equalityComparer, bool allowCancel, Page selectionPage, IReadOnlyCollection<IUser> users,
        IReadOnlyCollection<ButtonOption<T>> options, Page canceledPage, Page timeoutPage, Page successPage, DeletionOptions deletion,
        InputType inputType, ActionOnStop actionOnCancellation, ActionOnStop actionOnTimeout, ActionOnStop actionOnSuccess)
        : base(emoteConverter, stringConverter, equalityComparer, allowCancel, selectionPage, users, options, canceledPage,
            timeoutPage, successPage, deletion, inputType, actionOnCancellation, actionOnTimeout, actionOnSuccess)
    {
    }

    // This method needs to be overriden to build our own component the way we want.
    public override MessageComponent BuildComponents(bool disableAll)
    {
        var builder = new ComponentBuilder();
        foreach (var option in Options)
        {
            var emote = EmoteConverter?.Invoke(option);
            string label = StringConverter?.Invoke(option);
            if (emote is null && label is null)
            {
                throw new InvalidOperationException($"Neither {nameof(EmoteConverter)} nor {nameof(StringConverter)} returned a valid emote or string.");
            }

            var button = new ButtonBuilder()
                         .WithCustomId(emote?.ToString() ?? label)
                         .WithStyle(option.Style) // Use the style of the option
                         .WithEmote(emote)
                         .WithDisabled(disableAll);

            if (label is not null)
                button.Label = label;

            builder.WithButton(button);
        }

        return builder.Build();
    }
}

public record ButtonOption<T>(T Option, ButtonStyle Style); // An option with an style