using System;
using System.Threading.Tasks;
using BotCatMaxy;
using BotCatMaxy.Models;

namespace Discord.Commands
{
    public class RequireHierarchyAttribute : ParameterPreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
            ParameterInfo parameter, object value, IServiceProvider services)
        {
            if (context.User is not IGuildUser guildUser)
                return PreconditionResult.FromError("This command cannot be used outside of a guild");
            var targetUser = value switch
            {
                UserRef userRef => userRef.GuildUser,
                IGuildUser targetGuildUser => targetGuildUser,
                ulong userId => await context.Guild.GetUserAsync(userId),
                _ => throw new ArgumentOutOfRangeException("Unknown Type used in parameter that requires hierarchy"),
            };
            if (targetUser == null)
                if (value is UserRef)
                    return PreconditionResult.FromSuccess();
                else
                    return PreconditionResult.FromError("Target user not found");

            if (guildUser.GetHierarchy() <= targetUser.GetHierarchy())
                return PreconditionResult.FromError("You cannot target anyone else whose roles are higher than yours");

            var currentUser = await context.Guild.GetCurrentUserAsync();
            if (currentUser?.GetHierarchy() < targetUser.GetHierarchy())
                return PreconditionResult.FromError("The bot's role is lower than the targeted user.");

            return PreconditionResult.FromSuccess();
        }
    }
}