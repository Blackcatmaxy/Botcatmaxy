using BotCatMaxy.Data;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace BotCatMaxy.Models
{
    [BsonIgnoreExtraElements]
    public class ModerationSettings : DataObject
    {
        [BsonId]
        public string Id = "ModerationSettings";
        public List<ulong> ableToWarn = new List<ulong>();
        public List<ulong> cantBeWarned = new List<ulong>();
        public List<ulong> channelsWithoutAutoMod = new List<ulong>();
        public HashSet<string> allowedLinks = new HashSet<string>();
        public List<ulong> allowedToLink = new List<ulong>();
        public HashSet<string> badUEmojis = new HashSet<string>();
        public List<ulong> ableToBan = new List<ulong>();
        public List<ulong> anouncementChannels = new List<ulong>();
        public List<ulong> whitelistedForInvite = new List<ulong>();
        public TimeSpan? maxTempAction = null;
        public ulong mutedRole = 0;
        public ushort allowedCaps = 0;
        public bool useOwnerID = false;
        public bool invitesAllowed = true;
        public uint? maxEmojis = null;
        public bool moderateNames = false;
        public ushort? maxNewLines = null;
    }
}
