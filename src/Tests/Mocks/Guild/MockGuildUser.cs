using Discord;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Mocks.Guild
{
    public class MockGuildUser : MockUser, IGuildUser
    {
        public MockGuildUser(string username, IGuild guild, bool isBot = false) : base(username)
        {
            Guild = guild;
            IsBot = isBot;
        }

        List<IRole> roles = new();

        public Task RemoveTimeOutAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public DateTimeOffset? JoinedAt => throw new NotImplementedException();
        public string DisplayName { get; }

        public string Nickname => throw new NotImplementedException();
        public string DisplayAvatarId { get; }
        public string GuildAvatarId { get; }

        public GuildPermissions GuildPermissions
        {
            get
            {
                ulong resolvedPermissions = 0;
                if (Id == Guild.OwnerId)
                    resolvedPermissions = GuildPermissions.All.RawValue;
                else
                {
                    foreach (ulong roleId in RoleIds)
                    {
                        resolvedPermissions |= Guild.GetRole(roleId)?.Permissions.RawValue ?? 0;
                    }

                    const ulong flag = (ulong) GuildPermission.Administrator;
                    if ((resolvedPermissions & flag) == flag)
                        resolvedPermissions = GuildPermissions.All.RawValue;
                }
                return new GuildPermissions(resolvedPermissions);
            }
        }

        public IGuild Guild { get; set; }

        public ulong GuildId => Guild.Id;

        public DateTimeOffset? PremiumSince => throw new NotImplementedException();

        public IReadOnlyCollection<ulong> RoleIds => new ReadOnlyCollection<ulong>(roles.Select(role => role.Id).ToList());

        public bool? IsPending => throw new NotImplementedException();
        public int Hierarchy => throw new NotImplementedException();
        public DateTimeOffset? TimedOutUntil { get; }
        public GuildUserFlags Flags { get; }

        public bool IsDeafened => throw new NotImplementedException();

        public bool IsMuted => throw new NotImplementedException();

        public bool IsSelfDeafened => throw new NotImplementedException();

        public bool IsSelfMuted => throw new NotImplementedException();

        public bool IsSuppressed => throw new NotImplementedException();

        public IVoiceChannel VoiceChannel => throw new NotImplementedException();

        public string VoiceSessionId => throw new NotImplementedException();

        public bool IsStreaming => throw new NotImplementedException();
        public bool IsVideoing { get; }
        public DateTimeOffset? RequestToSpeakTimestamp { get; }

        public Task AddRoleAsync(ulong roleId, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task AddRoleAsync(IRole role, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task AddRolesAsync(IEnumerable<ulong> roleIds, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task AddRolesAsync(IEnumerable<IRole> roles, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task RemoveRoleAsync(ulong roleId, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public ChannelPermissions GetPermissions(IGuildChannel channel)
        {
            throw new NotImplementedException();
        }

        public string GetGuildAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            throw new NotImplementedException();
        }

        public string GetDisplayAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            throw new NotImplementedException();
        }

        public Task KickAsync(string reason = null, RequestOptions options = null)
        {
            Guild.AddBanAsync(this, reason: reason, options: options);
            Guild.RemoveBanAsync(this, options);
            return Task.CompletedTask;
        }

        public Task ModifyAsync(Action<GuildUserProperties> func, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task RemoveRoleAsync(IRole role, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task RemoveRolesAsync(IEnumerable<ulong> roleIds, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task RemoveRolesAsync(IEnumerable<IRole> roles, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task SetTimeOutAsync(TimeSpan span, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }
    }
}
