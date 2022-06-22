using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Fergun.Interactive;
using Fergun.Interactive.Selection;

namespace BotCatMaxy.Components.Interactivity;
//EXAMPLE FROM FERGUN.INTERACTIVE GITHUB https://github.com/d4n3436/Fergun.Interactive/commit/70d1af1c15408411c2cb7b6386a46480467debad#diff-a4bd3b09e045fea5a7d0691d56ae33e0c08fc7a98dd5f60006e4f2e5ab2fc43b

public class ButtonSelectionBuilder<T> : BaseSelectionBuilder<ButtonSelection<T>, ButtonOption<T>, ButtonSelectionBuilder<T>>
{
    // Since this selection specifically created for buttons, it makes sense to make this option the default.
    public override InputType InputType => InputType.Buttons;

    // We must override the Build method
    public override ButtonSelection<T> Build() => new(this);
}

// Custom selection where you can override the default button style/color
public class ButtonSelection<T> : BaseSelection<ButtonOption<T>>
{
    public ButtonSelection(ButtonSelectionBuilder<T> builder)
        : base(builder)
    {
    }

    // This method needs to be overriden to build our own component the way we want.
    public override ComponentBuilder GetOrAddComponents(bool disableAll, ComponentBuilder builder = null)
    {
        builder ??= new ComponentBuilder();
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

        return builder;
    }
}

public record ButtonOption<T>(T Option, ButtonStyle Style); // An option with an style