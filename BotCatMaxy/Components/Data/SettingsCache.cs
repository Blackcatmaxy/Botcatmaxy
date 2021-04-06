using BotCatMaxy.Models;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BotCatMaxy.Cache
{
    public class SettingsCache
    {
        public static volatile HashSet<GuildSettings> guildSettings = new();

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

    public class GuildSettings : IEquatable<GuildSettings>
    {
        private readonly IGuild guild;
        public ulong ID => guild.Id;
        public ModerationSettings moderationSettings;
        public TempActionList tempActionList;
        public ReportSettings reportSettings;
        public FilterSettings filterSettings;
        public BadWordList badWordList;
        public LogSettings logSettings;

        public GuildSettings(IGuild guild)
        {
            this.guild = guild;
        }

        public override bool Equals(object obj)
            => Equals(obj as GuildSettings);

        public bool Equals(GuildSettings other)
            => other?.ID == ID;

        public override int GetHashCode()
            => HashCode.Combine(ID);
    }
}
