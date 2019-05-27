using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;
using BotCatMaxy.Data;
using BotCatMaxy.Settings;
using System.Linq;
using System.IO;
using System.Collections;

namespace BotCatMaxy {
    public static class Utilities {
        public static string BasePath = "/home/bob_the_daniel/Data/";

        public static string ServerPath (this IGuild guild, bool createDir = true) {
            if (Directory.Exists(BasePath + guild.Id)) {
                return BasePath + guild.Id;
            } else if (Directory.Exists(BasePath + guild.OwnerId)) {
                return BasePath + guild.OwnerId;
            } else {
                if (createDir) {
                    Directory.CreateDirectory(BasePath + guild.OwnerId);
                    return BasePath + guild.OwnerId;
                } else {
                    return null;
                }
            }
        }
        public static bool HasAdmin(this SocketGuildUser user) {
            if (user.Guild.Owner == user) {
                return true;
            }

            bool hasAdmin = false;
            foreach (SocketRole role in (user).Roles) {
                if (role.Permissions.Administrator) {
                    hasAdmin = true;
                }
            }

            return hasAdmin;
        }
        
        public static bool CanWarn(this SocketGuildUser user) {
            if (HasAdmin(user)) {
                return true;
            }

            ModerationSettings settings = user.Guild.LoadModSettings(false);
            if (settings != null && settings.ableToWarn != null && settings.ableToWarn.Count > 0) {
                List<SocketRole> rolesAbleToWarn = new List<SocketRole>();
                foreach (ulong roleID in settings.ableToWarn) {
                    rolesAbleToWarn.Add(user.Guild.GetRole(roleID));
                }
                if (user.Roles.Intersect(rolesAbleToWarn).Any()) {
                    return true;
                }
            }
            foreach (SocketRole role in user.Roles) {
                if (role.Permissions.BanMembers) {
                    return true;
                }
            }

            return false;
        }

        public static SocketGuild GetGuild(SocketGuildChannel channel) {
            return channel.Guild;
        }
    }
}
