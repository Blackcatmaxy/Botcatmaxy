using System.Collections.Generic;
using BotCatMaxy.Data;
using Discord;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// A Dictionary with command names as key and guild perms as value, to store and load from the database
    /// </summary>
    public class CommandPermissions : DataObject
    {
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfArrays)]
        public Dictionary<ulong, List<string>> Map { get; } = new();

        public bool RoleHasValue(ulong role, string value)
        {
            if (!Map.TryGetValue(role, out var values))
                return false;
            return values != null && values.Contains(value);
        }
    }
}