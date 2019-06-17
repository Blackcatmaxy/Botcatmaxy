using System;
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
            string ownerPath = BasePath + guild.OwnerId;
            string guildPath = BasePath + guild.Id;
            if (Directory.Exists(guildPath)) {
                if (createDir) guildPath.CheckDirectories();
                return guildPath;
            } else if (Directory.Exists(ownerPath)) {
                if (createDir) ownerPath.CheckDirectories();
                return ownerPath;
            } else {
                if (createDir) {
                    Directory.CreateDirectory(ownerPath);
                    ownerPath.CheckDirectories();
                    return ownerPath;
                } else return null;
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

        public static bool CanBeWarned(this SocketGuildUser user) {
            if (HasAdmin(user)) return true;

            ModerationSettings settings = user.Guild.LoadModSettings(false);
            if (settings != null) {
                List<SocketRole> rolesAbleToBeWarned = new List<SocketRole>();
                foreach (ulong roleID in settings.ableToWarn) rolesAbleToBeWarned.Add(user.Guild.GetRole(roleID));
                if (user.Roles.Intersect(rolesAbleToBeWarned).Any()) return true;
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
            } else if (message.Severity == LogSeverity.Info) {
                Console.ForegroundColor = ConsoleColor.Cyan;
            }

            Console.WriteLine(message);
            Console.ResetColor();

            using (StreamWriter w = File.AppendText(BasePath + "log.txt")) {
                w.WriteLine(message);
            }
        }

        public static string Suffix(this int num) {
            if (num.ToString().EndsWith("11")) return num.ToString() + "th";
            if (num.ToString().EndsWith("12")) return num.ToString() + "th";
            if (num.ToString().EndsWith("13")) return num.ToString() + "th";
            if (num.ToString().EndsWith("1")) return num.ToString() + "st";
            if (num.ToString().EndsWith("2")) return num.ToString() + "nd";
            if (num.ToString().EndsWith("3")) return num.ToString() + "rd";
            return num.ToString() + "th";
        }

        public static async Task AssertAsync(this bool assertion, string message = "Assertion failed") {
            if (assertion == false) {
                await Log(new LogMessage(LogSeverity.Error, "Assert", message));
            }
        }

        public static async Task AssertWarnAsync(this bool assertion, string message = "Assertion failed") {
            if (assertion == false) {
                await Log(new LogMessage(LogSeverity.Warning, "Assert", message));
            }
        }
    }
}
