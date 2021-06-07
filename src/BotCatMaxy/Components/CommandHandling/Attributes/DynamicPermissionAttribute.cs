using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using BotCatMaxy.Models;

namespace Discord.Commands
{
    public class DynamicPermissionAttribute: PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
            IServiceProvider services)
        {
            //Makes sure it's in a server
            if (context.User is not IGuildUser gUser)
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));

            var guild = gUser.Guild;
            var permissionMap = guild.LoadFromFile<CommandPermissions>(false)?.map;
            List<ulong> roles = null;
            if (!permissionMap?.TryGetValue(command.Name, out roles)  ?? true)
            {
                //Check for default command value here
                return Task.FromResult(PreconditionResult.FromError("Permissions not set"));
            }
            
            if (!roles.Intersect(gUser.RoleIds).Any())
                return Task.FromResult(PreconditionResult.FromError("Missing permissions"));
            
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}