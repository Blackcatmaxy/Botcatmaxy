using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic    ;

namespace BotCatMaxy.Models
{
    public record Infraction
    {
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime Time { get; init; }
        public string LogLink { get; init; }
        public string Reason { get; init; }
        public float Size { get; init; }
    }

    [BsonIgnoreExtraElements]
    public class UserInfractions
    {
        [BsonId]
        public ulong ID { get; init; }
        public List<Infraction> infractions = new();
    }
}
