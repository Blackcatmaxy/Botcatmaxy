using System;
using BotCatMaxy.Services.TempActions;
using Discord;
using Discord.WebSocket;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace BotCatMaxy.Components.Logging
{
    public static class SerilogSinkExtensions
    {
        public static LoggerConfiguration TempActionSink(
            this LoggerSinkConfiguration loggerConfiguration, ITextChannel channel, LogEventLevel minLevel, out TempActionSink.FlushLogDelegate _flush,
            IFormatProvider formatProvider = null)
        {
            var sink = new TempActionSink(channel, formatProvider, out var flush);
            _flush = flush;
            return loggerConfiguration.Sink(sink, minLevel);
        }
    }
}