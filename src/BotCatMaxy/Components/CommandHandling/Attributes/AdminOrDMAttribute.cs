using System;
using System.Threading.Tasks;
using BotCatMaxy;

namespace Discord.Commands
{
    public class AdminOrDMAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
            IServiceProvider services)
        {
            // Check if the command was run in a server
            if (context.User is IGuildUser gUser)
            {
                // If this command was executed by a user with administrator permission, return a success
                if (gUser.HasAdmin())
                    return Task.FromResult(PreconditionResult.FromSuccess());
                else
                    return Task.FromResult(PreconditionResult.FromError("This command now only works in the bot's DMs"));
            }
            else
                return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}