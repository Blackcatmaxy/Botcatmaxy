using System;
using Discord;

namespace Tests.Commands.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class InsertRoleAttribute : Attribute
    {
        public string Name { get; }
        public string[] CommandNodes { get; }
        public GuildPermissions? Permissions { get; }

        public InsertRoleAttribute(string name, string[] commandNodes = null)
        {
            Name = name;
            CommandNodes = commandNodes;
        }

        public InsertRoleAttribute(string name, GuildPermission permission, string[] commandNodes = null) : this(name, commandNodes)
        {
            Permissions = new GuildPermissions((ulong)permission);
        }
    }
}