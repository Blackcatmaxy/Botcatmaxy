using BotCatMaxy.Data;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace BotCatMaxy.Models
{
    public class ReportSettings : DataObject
    {
        public TimeSpan? cooldown;
        public ulong? channelID;
        public ulong? requiredRole;
    }
}
