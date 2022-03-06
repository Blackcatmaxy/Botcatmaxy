using BotCatMaxy.Data;
using Discord;
using MongoDB.Bson.Serialization.Attributes;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// Settings for logging of messages inside of individual Discord servers
    /// </summary>
    public record LogSettings(IGuild Guild) : GuildDataRecord(Guild)
    {
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
