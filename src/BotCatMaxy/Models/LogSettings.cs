using BotCatMaxy.Data;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// Settings for logging of messages inside of individual Discord servers
    /// </summary>
    public class LogSettings : DataObject
    {
        [BsonId]
        public const string Id = "LogSettings";
        public ulong? pubLogChannel = null;
        public ulong? logChannel = null;
        public bool logDeletes = true;
        public bool logEdits = false;
        public bool logThreads = false;
        public ulong? backupChannel = null;

        public HashSet<ulong> channelLogBlacklist = new();

        public ulong? BestLog
        {
            get
            {
                return pubLogChannel ?? logChannel;
            }
        }
    }
}
