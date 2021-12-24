using System;
using System.Threading.Tasks;
using BotCatMaxy;

namespace Discord.Commands
{
    /// <summary>
    /// Requires the command to be executed by the owner of the current guild
    /// </summary>
    public class GuildOwnerAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
            IServiceProvider services)
        {
            //Make sure it's in a server
            if (context.User is IGuildUser gUser)
            {
                var guild = gUser.Guild;

                // If this command was executed by a user with an id that matches the guild owner's, return a success
                if (gUser.Id == guild.OwnerId)
                    return Task.FromResult(PreconditionResult.FromSuccess());
                else
                    return Task.FromResult(PreconditionResult.FromError("You don't have the permissions to use this."));
            }
            else
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
        }
    }
}