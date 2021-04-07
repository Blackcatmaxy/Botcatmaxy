using Discord;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// A way to "reference" users without always having a reference
    /// </summary>
    public record UserRef
    {
        public IGuildUser GuildUser { get; init; }
        public IUser User { get; init; }
        /// <summary>
        /// The snowflake identifier of the user, should always be valid
        /// </summary>
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
