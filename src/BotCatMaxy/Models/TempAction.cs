using System;
using System.Threading.Tasks;
using BotCatMaxy.Services.TempActions;
using Discord;
using MongoDB.Bson.Serialization.Attributes;

namespace BotCatMaxy.Models
{
    public abstract class TempAction
    {
        protected TempAction()
        {
            Start = DateTime.UtcNow;
        }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [BsonElement("dateBanned")]
        public DateTime Start { get; private set; }
        public string Reason { get; init; }
        [BsonElement("user")]
        public ulong UserId { get; init; }
        public TimeSpan Length { get; init; }
        public DateTime EndTime => Start.Add(Length);
        public bool ShouldEnd => DateTime.UtcNow >= EndTime;
        public abstract string LogString { get; }
        protected IUser CachedUser;

        /// <summary>
        /// Resolve the temp action
        /// </summary>
        public abstract Task ResolveAsync(IGuild guild, RequestOptions requestOptions);

        /// <summary>
        /// Verify temp action was resolved
        /// </summary>
        public abstract Task<bool> CheckResolvedAsync(IGuild guild, ResolutionType resolutionType, RequestOptions requestOptions);

        /// <summary>
        /// Log end of temp action and notify user
        /// </summary>
        public virtual async Task LogEndAsync(IGuild guild, IDiscordClient client, ResolutionType resolutionType)
        {
            bool isManual = resolutionType == ResolutionType.Early;
            string action = $"{(isManual ? "manually" : "auto")} untemp{LogString}ed";

            CachedUser ??= await client.GetUserAsync(UserId);
            CachedUser?.Notify(action, Reason, guild, client.CurrentUser);
            var userRef = CachedUser != null ? new UserRef(CachedUser) : new UserRef(UserId);
            await guild.LogEndTempAct(userRef, LogString, Reason, Length, isManual);
        }
    }

    public enum ResolutionType
    {
        Early,
        Normal
    }
}