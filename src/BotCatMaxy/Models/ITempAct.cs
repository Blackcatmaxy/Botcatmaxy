using System;
using System.Threading.Tasks;
using Discord;
using MongoDB.Bson.Serialization.Attributes;

namespace BotCatMaxy.Models
{
    public interface ITempAction
    {
        public string Reason { get; init; }

        public ulong UserId { get; init; }
        public TimeSpan Length { get; init; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime Start { get; init; }

        public DateTime EndTime => Start.Add(Length);

        public bool ShouldEnd => DateTime.UtcNow >= EndTime;

        public string LogString { get; }

        /// <summary>
        /// Resolve the temp action
        /// </summary>
        public abstract Task ResolveAsync(IGuild guild, RequestOptions requestOptions);

        /// <summary>
        /// Verify temp action was resolved
        /// </summary>
        public abstract Task VerifyResolvedAsync(IGuild guild, RequestOptions requestOptions);

        public Task LogEndAsync(IGuild guild, bool wasManual);
    }
}