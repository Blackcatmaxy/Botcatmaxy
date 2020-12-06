using Discord.WebSocket;

namespace BotCatMaxy.Models
{
    public record UserRef
    {
        public SocketGuildUser GuildUser { get; init; }
        public SocketUser User { get; init; }
        public ulong ID { get; init; }

        public UserRef(SocketGuildUser gUser)
        {
            GuildUser = gUser;
            User = gUser;
            ID = gUser.Id;
        }

        public UserRef(SocketUser user)
        {
            User = user;
            ID = user.Id;
        }

        public UserRef(ulong ID) => this.ID = ID;

        public UserRef(UserRef userRef, SocketGuild guild)
        {
            User = userRef.User;
            ID = userRef.ID;
            GuildUser = guild.GetUser(ID);
        }
    }
}
