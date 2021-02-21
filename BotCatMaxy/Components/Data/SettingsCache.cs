using BotCatMaxy.Models;
using Discord;
using Discord.WebSocket;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BotCatMaxy.Cache
{
    public class SettingsCache
    {
        public static HashSet<GuildSettings> guildSettings = new HashSet<GuildSettings>();
        public SettingsCache(IDiscordClient client) { 
            if (client is BaseSocketClient socketClient)
                socketClient.LeftGuild += RemoveGuild;
        }


        public Task RemoveGuild(SocketGuild guild)
        {
            guildSettings.RemoveWhere(g => g.ID == guild.Id);
            return Task.CompletedTask;
        }
    }

    public class GuildSettings
    {
        private readonly IGuild guild;
        public ulong ID => guild.Id;
        public ModerationSettings moderationSettings;
        public LogSettings logSettings;
        public TempActionList tempActionList;
        public BadWordList badWordList;
        public ReportSettings reportSettings;

        public GuildSettings(IGuild guild)
        {
            this.guild = guild;
        }
    }
}
