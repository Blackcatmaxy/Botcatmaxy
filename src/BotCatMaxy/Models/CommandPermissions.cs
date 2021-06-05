using System.Collections.Generic;
using BotCatMaxy.Data;
using Discord;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// A Dictionary with command names as key and guild perms as value, to store and load from the database
    /// </summary>
    public class CommandPermissions : DataObject
    {
        public Dictionary<string, GuildPermissions> map = new();
    }
}