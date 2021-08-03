using System;
using System.Collections.Generic;
using BotCatMaxy.Models;
using Serilog.Events;

namespace BotCatMaxy.Services.TempActions
{
    public static class CachedInfo
    {
        public static FixedSizedQueue<TimeSpan> CheckExecutionTimes { get; } = new(8);
        public static DateTime LastCheck { get; set; }
    }

    public static class CurrentInfo
    {
        public static bool Checking { get; set; }
        public static int CheckedGuilds { get; set; }
    }

    public record WrittenLogEvent
    {
        public DateTimeOffset TimeOffset { get; }
        public LogEventLevel EventLevel { get; }
        public string Content { get; }
        public Exception Exception { get; }
        public WrittenLogEvent(LogEventLevel level, string content, Exception exception = null)
        {
            EventLevel = level;
            Content = content;
            Exception = exception;
            TimeOffset = DateTimeOffset.UtcNow;
        }
    }
}