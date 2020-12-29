using BotCatMaxy.Data;
using System;
using System.Collections.Generic;

namespace BotCatMaxy.Models
{
    public class ModerationSettings : DataObject
    {
        public List<ulong> ableToWarn = new();
        public List<ulong> cantBeWarned = new();
        public List<ulong> channelsWithoutAutoMod = new();
        public HashSet<string> allowedLinks = new();
        public List<ulong> allowedToLink = new();
        public HashSet<string> badUEmojis = new();
        public HashSet<string> badLinks = new();
        public List<ulong> ableToBan = new();
        public List<ulong> anouncementChannels = new();
        public Dictionary<string, double> dynamicSlowmode = new();
        public List<ulong> whitelistedForInvite = new();
        public TimeSpan? maxTempAction = null;
        public ulong mutedRole = 0;
        public ushort allowedCaps = 0;
        public bool useOwnerID = false;
        public bool invitesAllowed = true;
        public bool zalgoAllowed = true;
        public uint? maxEmojis = null;
        public bool moderateNames = false;
        public ushort? maxNewLines = null;
    }
}