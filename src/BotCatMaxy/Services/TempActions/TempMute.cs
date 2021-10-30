using System;
using System.Threading.Tasks;
using BotCatMaxy.Models;
using Discord;

namespace BotCatMaxy.Services.TempActions
{
    public class TempMute : ITempAction
    {
        public string Reason { get; init; }
        public ulong UserId { get; init; }

        public ulong RoleId { get; init; }

        public TimeSpan Length { get; init; }

        public DateTime Start { get; init; }

        public string LogString => "mut";

        private IUser _cachedUser;

        public async Task ResolveAsync(IGuild guild, RequestOptions requestOptions)
        {
            var user = await guild.GetUserAsync(UserId, CacheMode.AllowDownload, requestOptions);
            if (user == null)
                return;

            _cachedUser = user;
            await user.RemoveRoleAsync(RoleId, requestOptions);
        }

        public async Task VerifyResolvedAsync(IGuild guild, RequestOptions requestOptions)
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

        public Task LogAsyncEnd(IGuild guild)
        {
            throw new NotImplementedException();
        }

        public Task LogEndAsync(IGuild guild, bool wasManual)
        {
            var userRef = _cachedUser != null ? new UserRef(_cachedUser) : new UserRef(UserId);
            return guild.LogEndTempAct(userRef, LogString, Reason, Length, wasManual);
        }
    }
}