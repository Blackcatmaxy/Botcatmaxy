using BotCatMaxy.Data;
using MongoDB.Bson.Serialization.Attributes;

namespace BotCatMaxy.Models
{
    [BsonIgnoreExtraElements]
    public class LogSettings : DataObject
    {
        [BsonId]
        public string Id = "LogSettings";
        public ulong? pubLogChannel = null;
        public ulong? logChannel = null;
        public bool logDeletes = true;
        public bool logEdits = false;
        public ulong? backupChannel = null;
        public ulong? BestLog
        {
            get
            {
                return pubLogChannel ?? logChannel;
            }
        }
    }
}
