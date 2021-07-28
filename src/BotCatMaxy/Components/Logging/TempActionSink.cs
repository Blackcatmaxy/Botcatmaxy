using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Serilog.Core;
using Serilog.Events;

namespace BotCatMaxy.Components.Logging
{
    public class TempActionSink : ILogEventSink
    {
        private MemoryStream _memoryStream;
        private readonly IFormatProvider _formatProvider;
        private readonly SocketTextChannel _channel;
        private readonly DiscordSocketClient _client;
        private ushort _events = 0;
        
        public delegate Task FlushLogDelegate();
        
        public TempActionSink(DiscordSocketClient client, IFormatProvider formatProvider, out FlushLogDelegate flush)
        {
            _memoryStream = new MemoryStream();
            const ulong logChannel = 866833376882589716;
            _formatProvider = formatProvider;
            _client = client;

            flush = FlushLog;
            _channel = _client.GetChannel(logChannel) as SocketTextChannel;
        }

        public void Emit(LogEvent logEvent)
        {
            _events++;
            ReadOnlySpan<byte> span = Encoding.UTF8.GetBytes(logEvent.RenderMessage());
            _memoryStream.Write(span);
            //Add new line
            _memoryStream.WriteByte(10);
        }

        private async Task FlushLog()
        {
            string log = Encoding.UTF8.GetString(_memoryStream.ToArray());
            _memoryStream.Position = 0;
            await _channel.SendFileAsync(_memoryStream, "TempActionCheck.log",
                $"Writing TempAction check log to file with {_events} lines and {_memoryStream.Length} bytes");

            _memoryStream = new MemoryStream();

            // byte[] buffer = _memoryStream.GetBuffer();
            // Array.Clear(buffer, 0, buffer.Length);
            // _memoryStream.Position = 0;
            // _memoryStream.SetLength(0);
            // _memoryStream.Capacity = 0;
        }
    }
}