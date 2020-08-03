using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BotCatMaxy.Startup
{
    public class DynamicSlowmode
    {
        public readonly DiscordSocketClient _client;
        public Timer timer;

        public DynamicSlowmode(DiscordSocketClient client)
        {
            _client = client;
            _client.Ready += OnReady;
        }

        public async Task OnReady()
        {
            _client.Ready -= OnReady;
            timer = new Timer((_) => _ = Check());
            timer.Change(0, 30 * 1000);
        }

        public async Task Check()
        {
            foreach (SocketGuild guild in _client.Guilds)
            {
                ModerationSettings settings = guild.LoadFromFile<ModerationSettings>();
                foreach (KeyValuePair<ulong, double> channelSetting in settings.dynamicSlowmode)
                {
                    // Key: channel id
                    // Value: factor
                    SocketTextChannel channel = guild.GetTextChannel(channelSetting.Key);

                    var messages = await channel.GetMessagesAsync(150).FlattenAsync();
                    messages = messages.Where(msg => msg.Timestamp > DateTime.Now.AddMinutes(-1));

                    var count = messages.Count() * channelSetting.Value;
                    if (count < 2) count = 1;
                    await channel.ModifyAsync(c => c.SlowModeInterval = Convert.ToInt32(count));
                }
            }
        }
    }
}
