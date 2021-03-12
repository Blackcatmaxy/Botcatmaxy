using Discord;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Mocks
{
    public class MockDMChannel : MockMessageChannel, IDMChannel
    {
        public MockDMChannel(ISelfUser bot, IUser user) : base(bot, $"{user.Username}'s DM")
        {
            Recipients = new IUser[] { user, bot }.ToImmutableArray();
            Recipient = user;
        }

        public IUser Recipient { get; init; }

        public IReadOnlyCollection<IUser> Recipients { get; init; }

        public Task CloseAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }
    }
}
