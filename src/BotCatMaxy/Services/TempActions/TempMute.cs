using System;
using System.Threading.Tasks;
using BotCatMaxy.Models;
using Discord;

namespace BotCatMaxy.Services.TempActions
{
    public class TempMute : TempAction
    {
        public ulong RoleId { get; init; }

        protected override string LogString => "mut";

        public override async Task ResolveAsync(IGuild guild, RequestOptions requestOptions)
        {
            var user = await guild.GetUserAsync(UserId, CacheMode.AllowDownload, requestOptions);
            if (user == null)
                return;

            CachedUser = user;
            await user.RemoveRoleAsync(RoleId, requestOptions);
        }

        public override async Task<bool> CheckResolvedAsync(IGuild guild, ResolutionType resolutionType, RequestOptions requestOptions)
        {
            var user = await guild.GetUserAsync(UserId, CacheMode.AllowDownload, requestOptions);
            if (user == null)
            // If checking for early ends, and user missing then not resolved.
            // If checking for normal ends, and user missing then is resolved.
                return resolutionType == ResolutionType.Normal;

            foreach (ulong roleId in user.RoleIds)
            {
                if (roleId == RoleId)
                    return false;
            }

            return true;
        }
    }
}