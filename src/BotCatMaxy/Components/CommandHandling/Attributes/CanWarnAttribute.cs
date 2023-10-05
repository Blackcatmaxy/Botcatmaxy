using System;
using System.Threading.Tasks;
using BotCatMaxy;
using Discord.Commands;

namespace Discord.Commands
{
    public class CanWarnAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
            IServiceProvider services)
        {
            //Makes sure it's in a server
            if (context.User is IGuildUser gUser)
            {
                // If this command was executed by a user with the appropriate role, return a success
                if (gUser.CanWarn())
                    return Task.FromResult(PreconditionResult.FromSuccess());
                else
                    return Task.FromResult(PreconditionResult.FromError("You don't have the permissions to use this."));
            }
            else
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
        }
    }
}

namespace Discord.Interactions
{
    public class CanWarnAttribute : Discord.Interactions.PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
        {
            //Makes sure it's in a server
            if (context.User is IGuildUser gUser)
            {
                // If this command was executed by a user with the appropriate role, return a success
                if (gUser.CanWarn())
                    return Task.FromResult(PreconditionResult.FromSuccess());
                else
                    return Task.FromResult(PreconditionResult.FromError("You don't have the permissions to use this."));
            }
            else
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
        }
    }
}