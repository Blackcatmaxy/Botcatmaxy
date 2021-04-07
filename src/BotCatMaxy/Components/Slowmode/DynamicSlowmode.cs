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
            timer.Change(1000, 15 * 1000);
        }

        public async Task Check()
        {
            try
            {
                foreach (SocketGuild guild in _client.Guilds)
                {
                    ModerationSettings settings = guild.LoadFromFile<ModerationSettings>();
                    if (settings?.dynamicSlowmode == null) continue;
                    foreach (KeyValuePair<string, double> channelSetting in settings.dynamicSlowmode)
                    {
                        // Key: channel id
                        // Value: factor
                        SocketTextChannel channel = guild.GetTextChannel(Convert.ToUInt64(channelSetting.Key));

                        var messages = await channel.GetMessagesAsync(200).FlattenAsync();
                        messages = messages.Where(msg => msg.GetTimeAgo() < TimeSpan.FromMinutes(1));

                        var count = messages.Count() * channelSetting.Value;
                        //API limit
                        if (count >= 21599) count = 21599;
                        if (count < 2) count = 1;
                        await channel.ModifyAsync(c => c.SlowModeInterval = Convert.ToInt32(count));
                    }
                }
            }
            catch (Exception e)
            {
                await new LogMessage(LogSeverity.Error, "Slow", "Something went wrong in slowmode", e).Log();
            }
        }
    }
}
