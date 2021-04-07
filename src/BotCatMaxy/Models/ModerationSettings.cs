using BotCatMaxy.Data;
using System;
using System.Collections.Generic;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// Settings for moderation of individual Discord servers
    /// </summary>
    public class ModerationSettings : DataObject
    {
        public List<ulong> ableToWarn = new();
        public List<ulong> ableToBan = new();
        public Dictionary<string, double> dynamicSlowmode = new();
        public TimeSpan? maxTempAction = null;
        public ulong mutedRole = 0;
        public bool useOwnerID = false;
    }
}