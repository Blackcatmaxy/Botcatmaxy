using Discord;

namespace BotCatMaxy.Models
{
    public record UserRef
    {
        public IGuildUser GuildUser { get; init; }
        public IUser User { get; init; }
        public ulong ID { get; init; }

        public UserRef(IGuildUser gUser)
        {
            GuildUser = gUser;
            User = gUser;
            ID = gUser.Id;
        }

        public UserRef(IUser user)
        {
            User = user;
            ID = user.Id;
        }

        public UserRef(ulong ID) => this.ID = ID;
    }
}
