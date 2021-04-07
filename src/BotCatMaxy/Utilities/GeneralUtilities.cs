using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Humanizer;
using MongoDB.Bson;
using MongoDB.Driver;
using Polly;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    public static class GeneralUtilities
    {
        public static IMongoCollection<BsonDocument> GetCollection(this IGuild guild, bool createDir = true)
        {
            if (guild == null)
                throw new ArgumentNullException(nameof(guild));
            var db = MainClass.dbClient.GetDatabase("Settings");
            var guildCollection = db.GetCollection<BsonDocument>(guild.Id.ToString());
            var ownerCollection = db.GetCollection<BsonDocument>(guild.OwnerId.ToString());
            if (guildCollection.CountDocuments(new BsonDocument()) > 0)
                return guildCollection;
            else if (ownerCollection.CountDocuments(new BsonDocument()) > 0)
                return ownerCollection;
            else if (createDir)
                return guildCollection;

            return null;
        }

        public static List<ulong> RoleIDs(this SocketGuildUser user)
        {
            return user.Roles.Select(role => role.Id).ToList();
        }

        public static string Suffix(this int num)
        {
            if (num.ToString().EndsWith("11")) return num.ToString() + "th";
            if (num.ToString().EndsWith("12")) return num.ToString() + "th";
            if (num.ToString().EndsWith("13")) return num.ToString() + "th";
            if (num.ToString().EndsWith("1")) return num.ToString() + "st";
            if (num.ToString().EndsWith("2")) return num.ToString() + "nd";
            if (num.ToString().EndsWith("3")) return num.ToString() + "rd";
            return num.ToString() + "th";
        }

        public static bool IsNullOrEmpty(this string s)
        {
            if (s == null || s == "")
                return true;
            return false;
        }

        public static string StrippedOfPing(this string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '@')
                {
                    if (s.ToArray()[sb.Length] != ' ') sb.Append('a');
                }
                else sb.Append(c);
            }

            return sb.ToString();
        }

        public static string NickOrUsername(this SocketGuildUser user)
        {
            if (user == null)
            {
                new LogMessage(LogSeverity.Error, "Utility", "User is null").Log();
                return "``NULL USER``";
            }
            if (user.Nickname.IsNullOrEmpty()) return user.Username;
            else return user.Nickname;
        }

        public static string Pluralize(this string s, float num)
        {
            if (num == 1) return s;
            else return s.Pluralize();
        }

        public static void DeleteOrRespond(this IMessage message, string toSay, IGuild guild, LogSettings settings = null)
        {
            if (settings == null) settings = guild.LoadFromFile<LogSettings>(false);
            if (guild.GetChannelAsync(settings?.pubLogChannel ?? 0).Result == null) message.Channel.SendMessageAsync(toSay);
            else
            {
                DiscordLogging.deletedMessagesCache.Enqueue(message.Id);
                _ = message.DeleteAsync();
                guild.GetTextChannelAsync(settings.pubLogChannel ?? 0).Result.SendMessageAsync($"{message.Author.Mention}, {toSay}");
            }
        }

        public static string LimitedHumanize(this TimeSpan timeSpan, int precision = 2)
        {
            return timeSpan.Humanize(precision, maxUnit: Humanizer.Localisation.TimeUnit.Day, minUnit: Humanizer.Localisation.TimeUnit.Second);
        }

        public static TimeSpan GetTimeAgo(this IMessage message)
        {
            Contract.Requires(message != null);
            return DateTime.UtcNow - message.Timestamp;
        }

        public static bool TryGetChannel(this IGuild guild, ulong id, out IGuildChannel channel)
        {
            Contract.Requires(guild != null);
            channel = guild.GetChannelAsync(id).GetAwaiter().GetResult();
            return channel != null;
        }

        public static bool TryGetTextChannel(this IGuild guild, ulong? id, out ITextChannel channel)
        {
            channel = null;
            if ((id ?? 0) == 0) return false;
            channel = guild.GetTextChannelAsync(id ?? 0).GetAwaiter().GetResult();
            return channel != null;
        }

        public static string Name(this UserRef userRef, bool showIDWithUser = false, bool showRealName = false)
        {
            if (userRef == null) return "``ERROR``";
            string name = null;
            if (userRef.GuildUser?.Nickname != null)
            {
                name = userRef.GuildUser.Nickname.StrippedOfPing();
                if (showRealName) //done since people with nicknames might have an innapropriate name under the nickname
                    name += $" aka {userRef.GuildUser.Username.StrippedOfPing()}";
            }
            if (name == null && userRef.User != null) name = userRef.User.Username.StrippedOfPing();
            if (name != null)
            {
                if (showIDWithUser) name += $" ({userRef.ID})";
                return name;
            }
            return $"User with ID:{userRef.ID}";
        }

        public static string Mention(this UserRef userRef)
        {
            if (userRef == null) return "``ERROR``";
            if (userRef.User != null) return userRef.User.Mention;
            return $"User with ID:{userRef.ID}";
        }

        public static EmbedBuilder WithAuthor(this EmbedBuilder embed, UserRef userRef)
        {
            Contract.Requires(embed != null);
            if (userRef.User != null) embed.WithAuthor(userRef.User);
            else embed.WithAuthor($"Unknown user with ID:{userRef.ID}");
            return embed;
        }

        public static void RecordAct(this ulong userID, IGuild guild, TempAct tempAct, string type, string loglink = null)
        {
            var acts = userID.LoadActRecord(guild, true);
            acts.Add(new ActRecord()
            {
                type = type,
                length = tempAct.Length,
                logLink = loglink,
                reason = tempAct.Reason,
                time = tempAct.DateBanned
            });
            userID.SaveActRecord(guild, acts);
        }

        public static async Task<IUser> SuperGetUser(this DiscordSocketClient client, ulong ID)
        {
            IUser user = client.GetUser(ID);
            if (user != null) return user;
            var requestOptions = new RequestOptions() { RetryMode = RetryMode.AlwaysRetry };
            Func<Task<IUser>> func = async () =>
            {
                return await client.Rest.GetUserAsync(ID, requestOptions);
            };
            return await func.SuperGet();
        }

        public static readonly int[] ignoredHTTPErrors = { 500, 503, 530 };
        public static async Task<T> SuperGet<T>(this Func<Task<T>> action)
        {
            var result = await Policy
                        .Handle<HttpException>(e => ignoredHTTPErrors.Contains((int)e.HttpCode))
                        .RetryAsync(3)
                        .ExecuteAndCaptureAsync(action);
            return result.FinalHandledResult;
        }

        public static async Task<IGuildUser> SuperGetUser(this RestGuild guild, ulong ID)
        {
            var requestOptions = new RequestOptions() { RetryMode = RetryMode.AlwaysRetry };
            Func<Task<IGuildUser>> func = async () =>
            {
                return await guild.GetUserAsync(ID, requestOptions);
            };
            return await func.SuperGet();
            /*for (int i = 0; i < 3; i++) {
                try {

                    if (user == null) {
                        RestGuild restGuild = await client.Rest.GetGuildAsync(guild.Id, requestOptions);
                        user = await restGuild.GetUserAsync(ID, requestOptions);
                    }
                    return user;
                } catch (HttpException e) { //If error happens and either has failed 3 times or non 500, 503, or 530 (not logged in) error
                    if (i == 2 || (e.HttpCode != System.Net.HttpStatusCode.ServiceUnavailable && e.HttpCode != System.Net.HttpStatusCode.InternalServerError && (int)e.HttpCode != 530)) throw;
                }
            }*/
            throw new Exception("SuperGetUser ran out of tries without throwing proper exception?");
        }

        public static async Task<RestGuild> SuperGetRestGuild(this DiscordRestClient client, ulong ID)
        {
            var requestOptions = new RequestOptions() { RetryMode = RetryMode.AlwaysRetry };
            Func<Task<RestGuild>> func = async () =>
            {
                return await client.GetGuildAsync(ID, requestOptions);
            };
            return await func.SuperGet();
        }

        public static async Task<RestInviteMetadata> SuperGetInviteDataAsync(this DiscordSocketClient client, string invite)
        {
            var requestOptions = new RequestOptions() { RetryMode = RetryMode.AlwaysRetry };
            Func<Task<RestInviteMetadata>> func = async () =>
            {
                return await client.GetInviteAsync(invite, requestOptions);
            };
            return await func.SuperGet();
        }
    }

    public class FixedSizedQueue<T> : ConcurrentQueue<T>
    {
        private readonly object syncObject = new object();

        public int Size { get; private set; }

        public FixedSizedQueue(int size)
        {
            Size = size;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            lock (syncObject)
            {
                while (Count > Size)
                {
                    TryDequeue(out _);
                }
            }
        }
    }
}
