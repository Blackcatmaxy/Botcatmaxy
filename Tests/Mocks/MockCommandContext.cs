using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Tests.Mocks.Guild;

namespace Tests.Mocks
{
    public class MockCommandContext : ICommandContext
    {
        public MockCommandContext(MockDiscordClient client, IUserMessage message)
        {
            Client = client;
            Message = message;
            User = message.Author;
            Channel = message.Channel;
            Guild = (message.Channel as MockTextChannel)?.Guild;
        }

        public IDiscordClient Client { get; init; }

        public IGuild Guild { get; init; }

        public IMessageChannel Channel { get; init; }

        public IUser User { get; init; }

        public IUserMessage Message { get; init; }
    }
}
