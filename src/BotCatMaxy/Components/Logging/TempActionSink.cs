using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace BotCatMaxy.Components.Logging
{
    public class TempActionSink : ILogEventSink
    {
        private const string _format = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        private readonly ITextFormatter _textFormatter;
        private readonly DiscordSocketClient _client;
        private readonly SocketTextChannel _channel;
        private readonly MemoryStream _memoryStream;
        private readonly StreamWriter _streamWriter;
        private ushort _events = 0;

        public delegate Task FlushLogDelegate();

        public TempActionSink(DiscordSocketClient client, IFormatProvider formatProvider, out FlushLogDelegate flush)
        {
            _memoryStream = new MemoryStream();
            const ulong logChannel = 866833376882589716;
            _client = client;

            flush = FlushLog;
            _channel = _client.GetChannel(logChannel) as SocketTextChannel;
            _streamWriter = new StreamWriter(_memoryStream, Encoding.UTF8);
            _textFormatter = new MessageTemplateTextFormatter(_format, formatProvider);
        }

        public void Emit(LogEvent logEvent)
        {
            _events++;
            _textFormatter.Format(logEvent, _streamWriter);
        }

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