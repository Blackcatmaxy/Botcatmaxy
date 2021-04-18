using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    public static class PermissionUtilities
    {
        /// <summary>
        /// If an <see cref="IGuildUser"/> has administrator permission or is owner
        /// </summary>
        public static bool HasAdmin(this IGuildUser user)
        {
            if (user == null) return false;
            
            return user.GuildPermissions.Administrator;
        }

        /// <summary>
        /// If an <see cref="IGuildUser"/> has kick permission or a role in warn ability list
        /// </summary>
        public static bool CanWarn(this IGuildUser user)
        {
            if (user == null) return false;

            if (user.GuildPermissions.KickMembers)
            {
                return true;
            }

            var settings = user.Guild.LoadFromFile<ModerationSettings>();
            if (settings?.ableToWarn != null && settings.ableToWarn.Count > 0)
            {
                if (user.RoleIds.Intersect(settings.ableToWarn).Any())
                {
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// If the focus has the role position to act on the comparer
        /// </summary>
        /// <param name="focus">If true, has the higher role</param>
        /// <param name="comparer">If true, has the lower role</param>
        public static bool CanActOn(this IGuildUser focus, IGuildUser comparer)
        {
            var focusPositions = focus.GetRoles()
                .Select(role => role.Position);
            var comparerPositions = comparer.GetRoles()
                .Select(role => role.Position);

            return focusPositions.Max() > comparerPositions.Max();
        }

        /// <summary>
        /// Returns all the roles of a <see cref="IGuildUser"/>
        /// </summary>
        public static IEnumerable<IRole> GetRoles(this IGuildUser user)
        {
            if (user is SocketGuildUser gUser)
                return gUser.Roles;

            return user.RoleIds.Select(id =>
                user.Guild.Roles.First(role => role.Id == id));
        }
        
        /// <summary>
        /// Returns the role position of an <see cref="IGuildUser"/>
        /// </summary>
        public static int GetHierarchy(this IGuildUser user)
        {
            if (user is SocketGuildUser socketUser)
                return socketUser.Hierarchy;

            if (user.Guild.OwnerId == user.Id)
                return int.MaxValue;

            if (user.RoleIds.Count == 0) return 0;
            return user.RoleIds.Max(role => user.Guild.GetRole(role).Position);
        }

        /// <summary>
        /// Returns the mutual guilds of a <see cref="IGuildUser"/>
        /// </summary>
        public static async Task<IReadOnlyCollection<IGuild>> GetMutualGuildsAsync(this IUser user,
            IDiscordClient client)
        {
            if (user is SocketUser socketUser)
                return socketUser.MutualGuilds;

            var guilds = await client.GetGuildsAsync();
            var result = new List<IGuild>(1);
            foreach (IGuild guild in guilds)
                if (await guild.GetUserAsync(user.Id) != null)
                    result.Add(guild);
            return result.AsReadOnly();
        }
    }
}