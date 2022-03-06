using Discord;

namespace BotCatMaxy.Models;
#nullable enable

public interface IGuildData
{
    public ulong GuildId { get; }

    public IGuild Guild { get; init; }
}

public record GuildDataRecord : IGuildData
{
    protected GuildDataRecord(IGuild guild)
    {
        Guild = guild;
    }

    public ulong GuildId => Guild.Id;

    [Newtonsoft.Json.JsonIgnore]
    public IGuild Guild { get; init; }
}