﻿using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;
using BotCatMaxy.Data;
using BotCatMaxy.Settings;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace BotCatMaxy {
    public static class Utilities {
        public static string BasePath = "/home/bob_the_daniel/Data/";

        public static string GetPath (this IGuild guild, bool createDir = true) {
            if (Directory.Exists(BasePath + guild.Id)) {
                if (createDir) guild.CheckDirectories();
                return BasePath + guild.Id;
            } else if (Directory.Exists(BasePath + guild.OwnerId)) {
                if (createDir) guild.CheckDirectories();
                return BasePath + guild.OwnerId;
            } else {
                if (createDir) {
                    Directory.CreateDirectory(BasePath + guild.OwnerId);
                    guild.CheckDirectories();
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

        public static void RemoveNullEntries(this IList list) {
            if (list != null || list.Count > 0) {
                foreach (object thing in list) {
                    if (thing == null) {
                        list.Remove(thing);
                    }
                }
            }
        }

        public static async Task Log(this LogMessage message) {
            if (message.Severity == LogSeverity.Error || message.Severity == LogSeverity.Critical) {
                Console.ForegroundColor = ConsoleColor.Red;
            } else if (message.Severity == LogSeverity.Warning) {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }
            
            Console.WriteLine(message);
            Console.ResetColor();

            using (StreamWriter w = File.AppendText(BasePath + "log.txt")) {
                w.WriteLine(message);
            }
        }
    }
}
