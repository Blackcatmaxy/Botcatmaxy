using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace BotCatMaxy.Models
{
    public class ActRecord
    {
        public string type;
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime time;
        public string logLink;
        public string reason;
        public TimeSpan length;
    }

    public class UserActs
    {
        [BsonId]
        public ulong ID = 0;
        public List<ActRecord> acts = new List<ActRecord>();
    }
}
