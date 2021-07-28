using System;
using System.Collections.Generic;
using BotCatMaxy.Models;

namespace BotCatMaxy.Services.TempActions
{
    public static class CachedInfo
    {
        public static FixedSizedQueue<TimeSpan> CheckExecutionTimes { get; } = new(8);
        public static DateTime LastCheck { get; set; }
    }

    public static class CurrentInfo
    {
        public static bool Checking { get; set; } = false;
        public static int CheckedGuilds { get; set; } = 0;
        public static uint CheckedMutes { get; set; }= 0;
        public static List<TempAct> EditedBans { get; set; } = null;
    }
}