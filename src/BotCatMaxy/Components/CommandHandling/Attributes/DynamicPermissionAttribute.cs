using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using BotCatMaxy.Models;

namespace Discord.Commands
{
    public class DynamicPermissionAttribute: PreconditionAttribute
    {
        public string Node { get; }
        
        public DynamicPermissionAttribute(string node)
        {
            Node = node;
        }
        
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
            IServiceProvider services)
        {
            //Makes sure it's in a server
            if (context.User is not IGuildUser gUser)
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));

            var guild = gUser.Guild;
            var permissions = guild.LoadFromFile<CommandPermissions>(false);
            if (permissions == null)
                return Task.FromResult(PreconditionResult.FromError("Permissions not set."));
            
            foreach (ulong role in gUser.RoleIds)
            {
                if (permissions.RoleHasValue(role, Node))
                    return Task.FromResult(PreconditionResult.FromSuccess());
            }
            return Task.FromResult(PreconditionResult.FromError($"Missing role with permission `{Node}` to use this command."));
        }
    }
}