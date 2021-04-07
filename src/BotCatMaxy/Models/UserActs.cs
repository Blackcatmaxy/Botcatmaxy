using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// A single recorded act
    /// </summary>
    public class ActRecord
    {
        public string type;
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime time;
        public string logLink;
        public string reason;
        public TimeSpan length;
    }

    /// <summary>
    /// A collection of <seealso cref="ActRecord"/>s to store and load from the database,
    /// </summary>
    public class UserActs
    {
        [BsonId]
        public ulong ID { get; init; }
        public List<ActRecord> acts = new();
    }
}
