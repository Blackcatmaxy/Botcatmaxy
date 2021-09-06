using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Tests.Mocks.Guild;

namespace Tests.Mocks
{
    public class MockDiscordClient : IDiscordClient, IDisposable
    {
        public MockDiscordClient()
        {

        }

        public List<MockGuild> guilds = new List<MockGuild>();

        public ConnectionState ConnectionState => throw new NotImplementedException();

        public ISelfUser CurrentUser => new MockSelfUser();

        public TokenType TokenType => throw new NotImplementedException();

        public Task<IGuild> CreateGuildAsync(string name, IVoiceRegion region, Stream jpegIcon = null, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task<IApplication> GetApplicationInfoAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<BotGateway> GetBotGatewayAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IChannel> GetChannelAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IConnection>> GetConnectionsAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IApplicationCommand> GetGlobalApplicationCommandAsync(ulong id, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IApplicationCommand>> GetGlobalApplicationCommandsAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IApplicationCommand> CreateGlobalApplicationCommand(ApplicationCommandProperties properties, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IApplicationCommand>> BulkOverwriteGlobalApplicationCommand(ApplicationCommandProperties[] properties, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IDMChannel>> GetDMChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IGroupChannel>> GetGroupChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IGuild> GetGuildAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            var guild = guilds.FirstOrDefault(g => g.Id == id);
            return Task.FromResult(guild as IGuild);
        }

        public Task<IReadOnlyCollection<IGuild>> GetGuildsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            IReadOnlyCollection<IGuild> collection = new ReadOnlyCollection<MockGuild>(guilds);
            return Task.FromResult(collection);
        }

        public Task<IInvite> GetInviteAsync(string inviteId, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IPrivateChannel>> GetPrivateChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetRecommendedShardCountAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IUser> GetUserAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return Task.FromResult<IUser>(null);
        }

        public Task<IUser> GetUserAsync(string username, string discriminator, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IVoiceRegion> GetVoiceRegionAsync(string id, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IVoiceRegion>> GetVoiceRegionsAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IWebhook> GetWebhookAsync(ulong id, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task StartAsync()
        {
            throw new NotImplementedException();
        }

        public Task StopAsync()
        {
            throw new NotImplementedException();
        }
    }
}
