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
        public static bool HasAdmin(this IGuildUser user)
        {
            if (user == null) return false;
            if (user.Guild.OwnerId == user.Id)
            {
                return true;
            }

            foreach (ulong id in user.RoleIds)
            {
                if (user.Guild.Roles.First(role => role.Id == id)
                    .Permissions.Administrator)
                    return true;
            }
            return false;
        }

        public static bool CanWarn(this IGuildUser user)
        {
            if (HasAdmin(user))
            {
                return true;
            }

            foreach (IRole role in user.GetRoles())
            {
                if (role.Permissions.KickMembers)
                {
                    return true;
                }
            }
            ModerationSettings settings = user.Guild.LoadFromFile<ModerationSettings>();
            if (settings != null && settings.ableToWarn != null && settings.ableToWarn.Count > 0)
            {
                if (user.RoleIds.Intersect(settings.ableToWarn).Any())
                {
                    return true;
                }
            }
            return false;
        }

        //Naming violation, should be WarnImmune
        public static bool CantBeWarned(this IGuildUser user)
        {
            if (user == null) return false;
            if (user.HasAdmin()) return true;
            return false;
        }

        public static bool CanActOn(this IGuildUser focus, IGuildUser comparer)
        {
            var focusPositions = focus.GetRoles()
                .Select(role => role.Position);
            var comparerPositions = comparer.GetRoles()
                .Select(role => role.Position);

            if (focusPositions.Max() > comparerPositions.Max())
                return true;
            return false;
        }

        public static IEnumerable<IRole> GetRoles(this IGuildUser user)
        {
            if (user is SocketGuildUser gUser)
                return gUser.Roles;

            return user.RoleIds.Select(id =>
                user.Guild.Roles.First(role => role.Id == id));
        }

        public static int GetHierarchy(this IGuildUser user)
        {
            if (user is SocketGuildUser socketUser)
                return socketUser.Hierarchy;

            if (user.Guild.OwnerId == user.Id)
                return int.MaxValue;

            if (user.RoleIds.Count == 0) return 0;
            return user.RoleIds.Max(role => user.Guild.GetRole(role).Position);
        }

        public static async Task<IReadOnlyCollection<IGuild>> GetMutualGuildsAsync(this IUser user, IDiscordClient client)
        {
            if (user is SocketUser socketUser)
                return socketUser.MutualGuilds;

            var guilds = await client.GetGuildsAsync();
            var result = new List<IGuild>(1);
            foreach (var guild in guilds)
                if (await guild.GetUserAsync(user.Id) != null)
                    result.Add(guild);
            return result.ToImmutableArray();
        }
    }
}
