using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
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

        public static bool CanWarn(this SocketGuildUser user)
        {
            if (HasAdmin(user))
            {
                return true;
            }

            foreach (SocketRole role in user.Roles)
            {
                if (role.Permissions.KickMembers)
                {
                    return true;
                }
            }
            ModerationSettings settings = user.Guild.LoadFromFile<ModerationSettings>();
            if (settings != null && settings.ableToWarn != null && settings.ableToWarn.Count > 0)
            {
                if (user.RoleIDs().Intersect(settings.ableToWarn).Any())
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CantBeWarned(this SocketGuildUser user)
        {
            if (user == null) return false;
            if (user.HasAdmin()) return true;

            ModerationSettings settings = user.Guild.LoadFromFile<ModerationSettings>();
            if (settings != null)
            {
                List<SocketRole> rolesUnableToBeWarned = new List<SocketRole>();
                foreach (ulong roleID in settings.cantBeWarned) rolesUnableToBeWarned.Add(user.Guild.GetRole(roleID));
                if (user.Roles.Intersect(rolesUnableToBeWarned).Any()) return true;
            }
            return false;
        }

        public static bool CanActOn(this SocketGuildUser focus, SocketGuildUser comparer)
        {
            if (focus.Roles.Select(role => role.Position).Max() > comparer.Roles.Select(role => role.Position).Max())
                return true;
            return false;
        }
    }
}
