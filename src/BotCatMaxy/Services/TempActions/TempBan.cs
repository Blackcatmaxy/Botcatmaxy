using System;
using System.Threading.Tasks;
using BotCatMaxy.Models;
using Discord;

namespace BotCatMaxy.Services.TempActions
{
    public class TempBan : TempAction
    {
        protected override string LogString => "bann";

        public override Task ResolveAsync(IGuild guild, RequestOptions requestOptions)
        {
            return guild.RemoveBanAsync(UserId, requestOptions);
        }

        public override async Task VerifyResolvedAsync(IGuild guild, RequestOptions requestOptions)
        {
            var ban = await guild.GetBanAsync(UserId, requestOptions);
            if (ban == null)
                return;

            CachedUser = ban.User;
            throw new Exception($"User:{UserId} still banned!");
        }
    }
}