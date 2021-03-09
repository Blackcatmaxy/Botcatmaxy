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
        public CommandResult(CommandError? error, string reason) : base(error, reason)
        {

        }

        public static CommandResult FromError(string reason)
            => new(CommandError.Unsuccessful, reason);

        public static CommandResult FromSuccess(string reason)
            => new(null, reason);
    }
}
