using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using BotCatMaxy;
using BotCatMaxy.Data;
using BotCatMaxy.Models;

namespace Discord.Commands
{
    public class CanWarnAttribute : PreconditionAttribute
    {
        public const string Node = "Moderation.Warn.Give";

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
            IServiceProvider services)
        {
            //Makes sure it's in a server
            if (context.User is not IGuildUser gUser)
                return Task.FromResult(PreconditionResult.FromError("This command can only be used in the context of a Discord server."));

            var permissions = gUser.Guild.LoadFromFile<CommandPermissions>();
            if (permissions.enabled)
            {
                foreach (ulong role in gUser.RoleIds.ToImmutableArray())
                {
                    if (permissions.RoleHasValue(role, Node))
                        return Task.FromResult(PreconditionResult.FromSuccess());
                }
                return Task.FromResult(PreconditionResult.FromError($"Missing role with permission `{Node}` to use this command."));
            }

            // If this command was executed by a user with the appropriate role, return a success
            if (gUser.CanWarn())
                return Task.FromResult(PreconditionResult.FromSuccess());
            else
                return Task.FromResult(PreconditionResult.FromError("You don't have the permissions to use this."));
        }
    }
}