using BotCatMaxy.Data;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// Settings for reports inside of individual Discord servers
    /// </summary>
    public class ReportSettings : DataObject
    {
        public TimeSpan? cooldown;
        public ulong? channelID;
        public ulong? requiredRole;
    }
}
