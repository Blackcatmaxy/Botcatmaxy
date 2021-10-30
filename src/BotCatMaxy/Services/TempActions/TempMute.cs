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

        public override async Task VerifyResolvedAsync(IGuild guild, RequestOptions requestOptions)
        {
            var user = await guild.GetUserAsync(UserId, CacheMode.AllowDownload, requestOptions);
            if (user == null)
                return;

            foreach (ulong roleId in user.RoleIds)
            {
                if (roleId == RoleId)
                    throw new Exception($"User:{UserId} still muted!");
            }
        }
    }
}