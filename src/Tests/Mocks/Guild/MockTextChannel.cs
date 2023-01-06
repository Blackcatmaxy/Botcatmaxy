using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace Tests.Mocks.Guild
{
    public class MockTextChannel : MockMessageChannel, ITextChannel
    {
        public MockTextChannel(ISelfUser user, IGuild guild, string name) : base(user, name)
        {
            Guild = guild;
        }

        IReadOnlyCollection<IGuildUser> users;

        public Task<IThreadChannel> CreateThreadAsync(string name, ThreadType type = ThreadType.PublicThread, ThreadArchiveDuration autoArchiveDuration = ThreadArchiveDuration.OneDay,
            IMessage message = null, bool? invitable = null, int? slowmode = null, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IThreadChannel>> GetActiveThreadsAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public bool IsNsfw => throw new NotImplementedException();

        public string Topic => throw new NotImplementedException();

        public int SlowModeInterval => throw new NotImplementedException();
        public ThreadArchiveDuration DefaultArchiveDuration { get; }

        public string Mention => throw new NotImplementedException();

        public ulong? CategoryId => null;

        public int Position => throw new NotImplementedException();
        public ChannelFlags Flags { get; }

        public IGuild Guild { get; init; }

        public ulong GuildId => Guild.Id;

        public IReadOnlyCollection<Overwrite> PermissionOverwrites => throw new NotImplementedException();

        public Task AddPermissionOverwriteAsync(IRole role, OverwritePermissions permissions, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task AddPermissionOverwriteAsync(IUser user, OverwritePermissions permissions, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IInviteMetadata> CreateInviteAsync(int? maxAge = 86400, int? maxUses = null, bool isTemporary = false, bool isUnique = false, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IInviteMetadata> CreateInviteToApplicationAsync(ulong applicationId, int? maxAge, int? maxUses = null, bool isTemporary = false,
            bool isUnique = false, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IInviteMetadata> CreateInviteToApplicationAsync(DefaultApplications application, int? maxAge, int? maxUses = null,
            bool isTemporary = false, bool isUnique = false, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IInviteMetadata> CreateInviteToStreamAsync(IUser user, int? maxAge, int? maxUses = null, bool isTemporary = false,
            bool isUnique = false, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IWebhook> CreateWebhookAsync(string name, Stream avatar = null, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task DeleteMessagesAsync(IEnumerable<IMessage> messages, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<ICategoryChannel> GetCategoryAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IInviteMetadata>> GetInvitesAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public OverwritePermissions? GetPermissionOverwrite(IRole role)
        {
            throw new NotImplementedException();
        }

        public OverwritePermissions? GetPermissionOverwrite(IUser user)
        {
            throw new NotImplementedException();
        }

        public Task<IWebhook> GetWebhookAsync(ulong id, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IWebhook>> GetWebhooksAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task ModifyAsync(Action<TextChannelProperties> func, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task ModifyAsync(Action<GuildChannelProperties> func, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task RemovePermissionOverwriteAsync(IRole role, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task RemovePermissionOverwriteAsync(IUser user, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task SyncPermissionsAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        Task<IGuildUser> IGuildChannel.GetUserAsync(ulong id, CacheMode mode, RequestOptions options)
        {
            throw new NotImplementedException();
        }

        public async Task DownloadUsers()
        {
            users = await Guild.GetUsersAsync();
        }

        IAsyncEnumerable<IReadOnlyCollection<IGuildUser>> IGuildChannel.GetUsersAsync(CacheMode mode, RequestOptions options)
        {
            IReadOnlyCollection<IGuildUser>[] collection = { users };
            return collection.ToAsyncEnumerable();
        }
    }
}
