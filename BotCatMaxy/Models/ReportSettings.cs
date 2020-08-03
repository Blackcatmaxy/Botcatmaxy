using BotCatMaxy.Data;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace BotCatMaxy.Models
{
    public class ReportSettings : DataObject
    {
        [BsonId]
        public string Id = "ReportSettings";
        public TimeSpan? cooldown;
        public ulong? channelID;
        public ulong? requiredRole;
    }
}
