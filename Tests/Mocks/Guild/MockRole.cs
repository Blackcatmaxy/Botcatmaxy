using System;
using System.Threading.Tasks;
using Discord;

namespace Tests.Mocks.Guild
{
    public class MockRole : IRole
    {
        public MockRole(string name, GuildPermissions permissions, int position, IGuild guild)
        {
            CreatedAt = DateTimeOffset.Now;
            Name = name;
            Permissions = permissions;
            Position = position;
            Guild = guild;
            var random = new Random();
            Id = (ulong)random.Next(0, int.MaxValue);
        }
        
        public ulong Id { get; }
        public DateTimeOffset CreatedAt { get; }
        public Task DeleteAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public string Mention { get; }
        public int CompareTo(IRole? other)
        {
            throw new NotImplementedException();
        }

        public Task ModifyAsync(Action<RoleProperties> func, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public IGuild Guild { get; }
        public string Name { get; }
        public int Position { get; }

        //Currently don't care about these
        public Color Color { get; }
        public bool IsHoisted { get; } = false;
        public bool IsManaged { get; } = false;
        public bool IsMentionable { get; } = false;
        public GuildPermissions Permissions { get; }
        public RoleTags Tags { get; }
    }
}