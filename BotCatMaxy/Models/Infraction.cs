using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace BotCatMaxy.Models
{
    public class Infraction
    {
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime time;
        public string logLink;
        public string reason;
        public float size;
    }

    [BsonIgnoreExtraElements]
    public class UserInfractions
    {
        [BsonId]
        public ulong ID = 0;
        public List<Infraction> infractions = new List<Infraction>();
    }
}
