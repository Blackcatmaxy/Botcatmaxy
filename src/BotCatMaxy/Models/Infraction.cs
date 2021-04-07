using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic    ;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// A single warning given to someone
    /// </summary>
    public record Infraction
    {
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime Time { get; init; }
        public string LogLink { get; init; }
        public string Reason { get; init; }
        public float Size { get; init; }
    }

    /// <summary>
    /// A collection of <seealso cref="Infraction"/>s to store and load from the database
    /// </summary>
    [BsonIgnoreExtraElements]
    public class UserInfractions
    {
        [BsonId]
        public ulong ID { get; init; }
        public List<Infraction> infractions = new();
    }
}
