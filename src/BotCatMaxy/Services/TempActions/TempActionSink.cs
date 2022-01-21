using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Templates;

namespace BotCatMaxy.Services.TempActions
{
    public class TempActionSink : ILogEventSink
    {
        private const string _format = "[{@t:mm:ss.fff} {@l:u3}]{#if IsDefined(GuildId)} {GuildId} {GuildIndex}:{#end} {@m:lj}\n{@x}";
        private readonly ITextFormatter _textFormatter;
        private readonly MemoryStream _memoryStream;
        private readonly StreamWriter _streamWriter;
        private readonly ITextChannel _channel;
        private ushort _events = 0;

        public delegate Task FlushLogDelegate();

        public TempActionSink(ITextChannel channel, IFormatProvider formatProvider, out FlushLogDelegate flush)
        {
            _memoryStream = new MemoryStream();
            _channel = channel;

            flush = FlushLog;
            _streamWriter = new StreamWriter(_memoryStream, new UTF8Encoding(false));
            _textFormatter = new ExpressionTemplate(_format, formatProvider);
        }

        public void Emit(LogEvent logEvent)
        {
            _events++;
            _textFormatter.Format(logEvent, _streamWriter);
        }

        /// <summary>
        /// Sends saved logs to Discord channel and resets to be able to save more logs.
        /// </summary>
        private async Task FlushLog()
        {
            await _streamWriter.FlushAsync();

            //Sending file disposes and we want to reuse _memoryStream
            var discordStream = new MemoryStream();
            //Need to reset position so the whole stream is included
            _memoryStream.Position = 0;
            await _memoryStream.CopyToAsync(discordStream);
            discordStream.Position = 0;
            await _channel.SendFileAsync(discordStream, "TempActionCheck.log",
                $"Writing TempAction check log to file with {_events} lines and {discordStream.Length} bytes.");

            _events = 0;
            //Resets the stream for reuse and for reuse of the same StreamWriter
            byte[] buffer = _memoryStream.GetBuffer();
            Array.Clear(buffer, 0, buffer.Length);
            _memoryStream.Position = 0;
            _memoryStream.SetLength(0);
            _memoryStream.Capacity = 0;
        }
    }
}