using System;
using System.Threading.Tasks;
using BotCatMaxy.Models;
using Discord;

namespace BotCatMaxy.Services.TempActions
{
    public class TempBan : ITempAction
    {
        public string Reason { get; init; }

        public ulong UserId { get; init; }

        public TimeSpan Length { get; init; }

        public DateTime Start { get; init; }

        public string LogString => "bann";

        private IUser _cachedUser;

        public Task ResolveAsync(IGuild guild, RequestOptions requestOptions)
        {
            return guild.RemoveBanAsync(UserId, requestOptions);
        }

        public async Task VerifyResolvedAsync(IGuild guild, RequestOptions requestOptions)
        {
            var ban = await guild.GetBanAsync(UserId, requestOptions);
            if (ban == null)
                return;

            _cachedUser = ban.User;
            throw new Exception($"User:{UserId} still banned!");
        }

        public Task LogEndAsync(IGuild guild, bool wasManual)
        {
            var userRef = _cachedUser != null ? new UserRef(_cachedUser) : new UserRef(UserId);
            return guild.LogEndTempAct(userRef, LogString, Reason, Length, wasManual);
        }
    }
}