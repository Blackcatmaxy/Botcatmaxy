using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BotCatMaxy.Startup;

namespace Discord.Commands
{
    /// <summary>
    /// A wrapper of <seealso cref="RuntimeResult"/> for communicating the result of a command in the <seealso cref="TextCommandHandler"/>
    /// </summary>
    #nullable enable
    public class CommandResult : RuntimeResult
    {
        public readonly Embed? Embed;
        public readonly string? LogLink;

        public CommandResult(CommandError? error, string reason, Embed? embed = null, string? logLink = null) : base(error, reason)
        {
            Embed = embed;
            LogLink = logLink;
        }

        public static CommandResult FromError(string reason, Embed? embed = null)
            => new(CommandError.Unsuccessful, reason, embed);

        public static CommandResult FromSuccess(string reason, Embed? embed = null, string? logLink = null)
            => new(null, reason, embed, logLink);
    }
}
