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
    /// A wrapper of <seealso cref="RuntimeResult"/> for communicating the result of a command in the <seealso cref="CommandHandler"/>
    /// </summary>
    public class CommandResult : RuntimeResult
    {
        public readonly Embed Embed;

        public CommandResult(CommandError? error, string reason, Embed embed = null) : base(error, reason)
        {
            Embed = embed;
        }

        public static CommandResult FromError(string reason, Embed embed = null)
            => new(CommandError.Unsuccessful, reason, embed);

        public static CommandResult FromSuccess(string reason, Embed embed = null)
            => new(null, reason, embed);
    }
}
