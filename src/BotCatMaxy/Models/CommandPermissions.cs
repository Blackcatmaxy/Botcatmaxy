using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Check if role has permission node
        /// </summary>
        /// <param name="role">Role ID</param>
        /// <param name="value">Permission node to check</param>
        public bool RoleHasValue(ulong role, string value)
        {
            if (!Map.TryGetValue(role, out var values) || values == null)
                return false;

            var valueSplit = value.Split('.');
            foreach (var roleValue in values)
            {
                if (roleValue.Equals(value, StringComparison.InvariantCultureIgnoreCase))
                    return true;
                var roleValueSplit = roleValue.Split('.').ToList();
                
                if (value.Length > roleValueSplit.Count && roleValueSplit[^1] != "*")
                    continue;
                
                if (roleValueSplit[^1] == "*")
                    roleValueSplit.Remove("*");

                bool result = true;
                for (int i = 0; i < roleValueSplit.Count; i++)
                {
                    if (!roleValueSplit[i].Equals(valueSplit[i], StringComparison.InvariantCultureIgnoreCase))
                    {
                        result = false;
                        break;
                    }
                }

                if (result)
                    return true;
            }

            return false;
        }
    }
}