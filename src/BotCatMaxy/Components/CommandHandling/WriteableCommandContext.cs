using Discord;
using Discord.Commands;

namespace BotCatMaxy.Components.CommandHandling
{
    public class WriteableCommandContext : ICommandContext
    {
        public IDiscordClient Client { get; set; }
        public IGuild Guild { get; set; }
        public IMessageChannel Channel { get; set; }
        public IUser User { get; set; }
        public IUserMessage Message { get; set; }

        public bool IsPrivate => Channel is IPrivateChannel;
    }
}