using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace BotCatMaxy.Components.Filter
{
    public class ReactionContext : ICommandContext
    {
        public IMessage Message { get; }
        public IDiscordClient Client { get; }
        public IUser User => Message.Author;
        public IMessageChannel Channel => Message.Channel;
        public IGuild Guild => (Channel as IGuildChannel)?.Guild;

        IUserMessage ICommandContext.Message => Message as IUserMessage;

        public ReactionContext(IDiscordClient client, IMessage message)
        {
            Message = message;
            Client = client;
        }

    }
}
