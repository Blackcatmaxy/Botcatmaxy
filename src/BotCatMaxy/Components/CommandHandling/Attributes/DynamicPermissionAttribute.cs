using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Humanizer;

namespace Discord.Commands
{
    public class DynamicPermissionAttribute: PreconditionAttribute
    {
        public string Node { get; }
        public GuildPermission? Fallback { get; }

        public DynamicPermissionAttribute(string node)
        {
            Node = node;
        }

        public DynamicPermissionAttribute(string node, GuildPermission fallback) : this(node)
        {
            Fallback = fallback;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
            IServiceProvider services)
        {
            //Makes sure it's in a server
            if (context.User is not IGuildUser gUser)
                return Task.FromResult(PreconditionResult.FromError("This command can only be used in the context of a Discord server."));

            var guild = gUser.Guild;
            var permissions = guild.LoadFromFile<CommandPermissions>(false);
            if (permissions?.enabled != true)
            {
                if (Fallback == null)
                    return Task.FromResult(PreconditionResult.FromError("Advanced Permission System not set up and required for this command."));

                if (gUser.GuildPermissions.Has(Fallback.Value))
                    return Task.FromResult(PreconditionResult.FromSuccess());
                return Task.FromResult(PreconditionResult.FromError($"You need server permission {Fallback.ToString().Humanize(LetterCasing.LowerCase)}."));
            }

            foreach (ulong role in gUser.RoleIds.ToImmutableArray())
            {
                if (permissions.RoleHasValue(role, Node))
                    return Task.FromResult(PreconditionResult.FromSuccess());
            }
            return Task.FromResult(PreconditionResult.FromError($"Missing role with permission `{Node}` to use this command."));
        }
    }
}