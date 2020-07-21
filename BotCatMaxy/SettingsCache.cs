using System;
using System.Collections.Generic;
using System.Text;
using BotCatMaxy.Data;
using BotCatMaxy;
using Discord.WebSocket;
using Discord;
using System.Linq;
using Discord.Rest;
using System.Threading.Tasks;
using BotCatMaxy.Models;

namespace BotCatMaxy.Cache
{
    public class SettingsCache
    {
        public static HashSet<GuildSettings> guildSettings = new HashSet<GuildSettings>();
        private DiscordSocketClient client;
        public SettingsCache(DiscordSocketClient client)
        {
            this.client = client;
            this.client.LeftGuild += RemoveGuild;
        }


        public async Task RemoveGuild(SocketGuild guild)
        {
            guildSettings.RemoveWhere(g => g.ID == guild.Id);
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
