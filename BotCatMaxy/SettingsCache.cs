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

namespace BotCatMaxy.Cache {
    public class SettingsCache {
        public static List<GuildSettings> guildSettings = new List<GuildSettings>();

        public void SetUp(DiscordSocketClient client) {
            client.LeftGuild += RemoveGuild;
        }

        public async Task RemoveGuild(SocketGuild guild) {
            guildSettings.RemoveAll(g => g.ID == guild.Id);
        }
    }

    public class GuildSettings {
        private readonly IGuild guild;
        public ulong ID => guild.Id;
        public ModerationSettings moderationSettings;
        public LogSettings logSettings;
        public TempActionList tempActionList;
        public BadWordList badWordList;
        public GuildSettings(IGuild guild) {
            this.guild = guild;
        }
    }
}
