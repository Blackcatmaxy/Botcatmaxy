using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;
using BotCatMaxy.Data;
using BotCatMaxy.Settings;
using System.Linq;
using System.Threading.Tasks;
using Discord.Rest;
using System.IO;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Collections;

namespace BotCatMaxy {
    public static class Utilities {
        public static string BasePath = "/home/bob_the_daniel/Data/";

        public static IMongoCollection<BsonDocument> GetCollection(this IGuild guild, bool createDir = true) {
            var db = MainClass.dbClient.GetDatabase("Settings");
            var guildCollection = db.GetCollection<BsonDocument>(guild.Id.ToString());
            var ownerCollection = db.GetCollection<BsonDocument>(guild.OwnerId.ToString());
            if (guildCollection.CountDocuments(new BsonDocument()) > 0) {
                return guildCollection;
            } else if (ownerCollection.CountDocuments(new BsonDocument()) > 0 || createDir) {
                return ownerCollection;
            }

            return null;
        }

        public static string GetPath(this IGuild guild, bool createDir = true) {
            /*string ownerPath = guild.OwnerId.ToString();
            string guildPath = guild.Id.ToString();
            using (LiteRepository guildRep = new LiteRepository(guildPath))
            using (LiteRepository ownerRep = new LiteRepository(guildPath)) {
                if (guildRep.SingleOrDefault<object>() != null) {
                    return guildPath;
                } else if (ownerRep.SingleOrDefault<object>() != null) {
                    return ownerPath;
                } else {
                    if (createDir) {
                        return ownerPath;
                    } else return null;
                }
            }*/
            return null;
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

            foreach (SocketRole role in user.Roles) {
                if (role.Permissions.BanMembers) {
                    return true;
                }
            }
            ModerationSettings settings = user.Guild.LoadFromFile<ModerationSettings>();
            if (settings != null && settings.ableToWarn != null && settings.ableToWarn.Count > 0) {
                List<SocketRole> rolesAbleToWarn = new List<SocketRole>();
                foreach (ulong roleID in settings.ableToWarn) {
                    rolesAbleToWarn.Add(user.Guild.GetRole(roleID));
                }
                if (user.Roles.Intersect(rolesAbleToWarn).Any()) {
                    return true;
                }
            }
            return false;
        }

        public static List<ulong> RoleIDs(this SocketGuildUser user) {
            List<ulong> IDs = new List<ulong>();
            foreach (SocketRole role in user.Roles) {
                IDs.Add(role.Id);
            }
            return IDs;
        }

        public static bool CantBeWarned(this SocketGuildUser user) {
            if (HasAdmin(user)) return true;

            ModerationSettings settings = user.Guild.LoadFromFile<ModerationSettings>();
            if (settings != null) {
                List<SocketRole> rolesUnableToBeWarned = new List<SocketRole>();
                foreach (ulong roleID in settings.cantBeWarned) rolesUnableToBeWarned.Add(user.Guild.GetRole(roleID));
                if (user.Roles.Intersect(rolesUnableToBeWarned).Any()) return true;
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

        public static bool IsNullOrEmpty(this string s) {
            if (s == null || s == "")
                return true;
            return false;
        }

        public static string StrippedOfPing(this string s) {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s) {
                if (c == '@') {
                    if (s.ToArray()[sb.Length] != ' ') sb.Append('a');
                } else sb.Append(c);
            }

            return sb.ToString();
        }

        public static string NickOrUsername(this SocketGuildUser user) {
            if (user == null) {
                new LogMessage(LogSeverity.Error, "Utility", "User is null");
                return "``NULL USER``";
            }
            if (user.Nickname.IsNullOrEmpty()) return user.Username;
            else return user.Nickname;
        }

        public static TimeSpan? ToTime(this string s) {
            try {
                string intString = s.Remove(s.Length - 1);
                if (s.ToLower().EndsWith('d')) {
                    return TimeSpan.FromDays(double.Parse(intString));
                } else if (s.ToLower().EndsWith('h')) {
                    return TimeSpan.FromHours(double.Parse(intString));
                }
            } catch {
            }
            return null;
        }

        public static bool IsNullOrEmpty(this IEnumerable<object> list) {
            if (list == null || list.ToArray().Length == 0) return true;
            else return false;
        }
    }
}
