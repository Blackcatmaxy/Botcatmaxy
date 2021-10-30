using System;
using System.Threading.Tasks;
using BotCatMaxy.Services.TempActions;
using Discord;
using MongoDB.Bson.Serialization.Attributes;

namespace BotCatMaxy.Models
{
    public abstract class TempAction
    {
        public string Reason { get; init; }
        public ulong UserId { get; init; }
        public TimeSpan Length { get; init; }
        public DateTime Start { get; } = DateTime.UtcNow;
        public DateTime EndTime => Start.Add(Length);
        public bool ShouldEnd => DateTime.UtcNow >= EndTime;
        protected abstract string LogString { get; }
        protected IUser CachedUser;

        /// <summary>
        /// Resolve the temp action
        /// </summary>
        public abstract Task ResolveAsync(IGuild guild, RequestOptions requestOptions);

        /// <summary>
        /// Verify temp action was resolved
        /// </summary>
        public abstract Task VerifyResolvedAsync(IGuild guild, RequestOptions requestOptions);

        /// <summary>
        /// Log end of temp action and notify user
        /// </summary>
        public virtual async Task LogEndAsync(IGuild guild, IDiscordClient client, bool wasManual)
        {
            string action = $"{(wasManual ? "auto" : "manually")} untemp{LogString}ed";

            CachedUser ??= await client.GetUserAsync(UserId);
            CachedUser?.Notify(action, Reason, guild, client.CurrentUser);
            var userRef = CachedUser != null ? new UserRef(CachedUser) : new UserRef(UserId);
            await guild.LogEndTempAct(userRef, LogString, Reason, Length, wasManual);
        }
    }
}