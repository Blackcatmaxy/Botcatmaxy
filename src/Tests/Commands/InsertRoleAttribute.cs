using System;
using Discord;

namespace Tests.Commands
{
    public class InsertRoleAttribute : Attribute
    {
        public ulong Id { get; }
        public string[] CommandNodes { get; }
        public GuildPermissions? Permissions { get; }

        public InsertRoleAttribute(ulong Id, string[] commandNodes = null, GuildPermissions? permissions = null)
        {
            this.Id = Id;
            CommandNodes = commandNodes;
            Permissions = permissions;
        }
    }
}