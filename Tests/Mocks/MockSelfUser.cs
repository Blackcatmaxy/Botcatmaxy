using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Mocks
{
    public class MockSelfUser : MockUser, ISelfUser
    {
        public MockSelfUser() : base("BotCatMaxy")
        {
            Username = "BotCatMaxy";
            IsBot = true;
        }
        public string Email => throw new NotImplementedException();

        public bool IsVerified => throw new NotImplementedException();

        public bool IsMfaEnabled => throw new NotImplementedException();

        public UserProperties Flags => throw new NotImplementedException();

        public PremiumType PremiumType => throw new NotImplementedException();

        public string Locale => throw new NotImplementedException();

        public Task ModifyAsync(Action<SelfUserProperties> func, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }
    }
}
