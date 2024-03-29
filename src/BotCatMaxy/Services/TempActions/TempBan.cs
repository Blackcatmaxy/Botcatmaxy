using System;
using System.Threading.Tasks;
using BotCatMaxy.Models;
using Discord;

namespace BotCatMaxy.Services.TempActions
{
    public class TempBan : TempAction
    {
        public TempBan() { }

        public TempBan(TimeSpan length, string reason, ulong userId)
        {
            Length = length;
            Reason = reason;
            UserId = userId;
        }

        public override TempActionType Type => TempActionType.TempBan;
        protected override string LogString => "bann";

        public override Task ResolveAsync(IGuild guild, RequestOptions requestOptions)
        {
            return guild.RemoveBanAsync(UserId, requestOptions);
        }

        public override async Task<bool> CheckResolvedAsync(IGuild guild, ResolutionType _, RequestOptions requestOptions)
        {
            var ban = await guild.GetBanAsync(UserId, requestOptions);
            if (ban == null)
                return true;

            CachedUser = ban.User;

            return false;
        }
    }
}