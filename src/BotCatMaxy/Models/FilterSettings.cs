using BotCatMaxy.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// Settings for the auto filter of individual Discord servers
    /// </summary>
    public class FilterSettings : DataObject
    {
        public HashSet<ulong> channelsWithoutAutoMod = new();
        public HashSet<ulong> whitelistedForInvite = new();
        public HashSet<ulong> announcementChannels = new();
        public HashSet<ulong> allowedToLink = new();
        public HashSet<ulong> filterIgnored = new();
        public HashSet<string> allowedLinks = new();
        public HashSet<string> badUEmojis = new();
        public HashSet<string> badLinks = new();

        public bool invitesAllowed = true;
        public bool moderateNames = false;
        public bool zalgoAllowed = true;
        public ushort? maxNewLines = null;
        public ushort allowedCaps = 0;
        public uint? maxEmojis = null;
    }
}
