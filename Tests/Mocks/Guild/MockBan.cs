using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Mocks.Guild
{
    public class MockBan : IBan
    {
        public MockBan(IUser user, string reason)
        {
            User = user;
            Reason = reason;
        }

        public IUser User { get; init; }

        public string Reason { get; init; }
    }
}
