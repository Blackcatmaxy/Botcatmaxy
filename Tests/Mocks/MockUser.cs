using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace Tests.Mocks
{
    public class MockUser : IUser
    {
        public MockUser(string username)
        {
            Username = username;
            IsBot = false;
            var random = new Random();
            Id = (ulong)random.Next(0, int.MaxValue);
        }

        MockDMChannel channel;
        public string AvatarId => throw new NotImplementedException();

        public string Discriminator => $"#{DiscriminatorValue}";

        public ushort DiscriminatorValue => 1234;

        public bool IsBot { get; init; }

        public bool IsWebhook => false;

        public string Username { get; init; }

        public UserProperties? PublicFlags => throw new NotImplementedException();

        public DateTimeOffset CreatedAt => throw new NotImplementedException();

        public ulong Id { get; init; }

        public string Mention => $"@{Username}";

        public IActivity Activity => throw new NotImplementedException();

        public UserStatus Status => throw new NotImplementedException();

        public IImmutableSet<ClientType> ActiveClients => throw new NotImplementedException();

        public IImmutableList<IActivity> Activities => throw new NotImplementedException();

        public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            throw new NotImplementedException();
        }

        public string GetDefaultAvatarUrl()
        {
            throw new NotImplementedException();
        }

        public Task<IDMChannel> GetOrCreateDMChannelAsync(RequestOptions options = null)
        {
            channel ??= new MockDMChannel(new MockSelfUser(), this);
            return Task.FromResult<IDMChannel>(channel);
        }
    }
}
